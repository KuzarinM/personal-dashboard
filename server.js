import express from 'express';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import ical from 'node-ical';
import axios from 'axios';
import https from 'https';
import dns from 'dns';
import { TelegramClient } from "telegram";
import { StringSession } from "telegram/sessions/index.js";

// --- 1. СЕТЬ ---
if (dns.setDefaultResultOrder) {
    try { dns.setDefaultResultOrder('ipv4first'); } catch(e) {}
}

// Увеличили таймаут агента до 30 секунд
const agent = new https.Agent({ 
    family: 4, 
    keepAlive: true, 
    timeout: 30000 
});

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 3000;
const DATA_DIR = path.join(__dirname, 'data');

if (!fs.existsSync(DATA_DIR)) {
    try { fs.mkdirSync(DATA_DIR); } catch(e) {}
}

app.use(express.json());

// --- 2. TELEGRAM (С КЕШЕМ) ---
const TG_API_ID = 2040;
const TG_API_HASH = "b18441a1ff607e10a989891a5462e627";
const TG_SESSION_FILE = path.join(DATA_DIR, 'telegram_session.txt');

// КЕШ ТЕЛЕГРАМА (Чтобы не ловить Flood Wait)
// Структура: Map<dashboardName, { data: [], timestamp: 12345678 }>
const tgCache = new Map();
const TG_CACHE_TTL = 15000; // 15 секунд (время жизни кеша)

let tgClient = null;
const activeTgClients = new Map();

const getTgClient = async (dashName, sessionString) => {
    try {
        if (activeTgClients.has(dashName)) {
            const client = activeTgClients.get(dashName);
            if (client.connected) return client;
        }
        const client = new TelegramClient(new StringSession(sessionString), TG_API_ID, TG_API_HASH, {
            connectionRetries: 5, useWSS: false
        });
        await client.connect();
        activeTgClients.set(dashName, client);
        return client;
    } catch (e) {
        console.error(`TG Init Error [${dashName}]:`, e.message);
        return null;
    }
};

// --- 3. HELPERS ---
const getFilePath = (name) => {
    const cleanName = (name || 'default').replace(/[^a-z0-9-_]/gi, '').toLowerCase();
    return path.join(DATA_DIR, `${cleanName}.json`);
};

const checkAuth = (req, res, config, dashName) => {
    const safeName = (dashName || 'default').toLowerCase();
    if (safeName === 'default' && req.method === 'GET') return true;

    const authHeader = req.headers.authorization;
    const safeRealm = Buffer.from(safeName).toString('base64');

    if (!authHeader) {
        res.set('WWW-Authenticate', `Basic realm="Dashboard_${safeRealm}"`);
        res.status(401).send('Auth Required');
        return false;
    }

    const b64auth = authHeader.split(' ')[1];
    const [login, password] = Buffer.from(b64auth, 'base64').toString().split(':');

    let validUser, validPass;
    const settings = config?.settings || {};
    
    if (safeName === 'default') {
        validUser = 'admin';
        validPass = settings.masterPassword || 'admin';
    } else {
        validUser = settings.auth?.username || 'admin';
        validPass = settings.auth?.password || 'password';
    }

    if (login !== validUser || password !== validPass) {
        res.set('WWW-Authenticate', `Basic realm="Dashboard_${safeRealm}"`);
        res.status(401).send('Access Denied');
        return false;
    }
    return true;
};

// --- 4. API ---

app.get('/api/dashboards', (req, res) => {
    try {
        const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json'));
        const names = files.map(f => f.replace('.json', ''));
        if (!names.includes('default')) names.unshift('default');
        res.json([...new Set(names)]);
    } catch (e) { res.json(['default']); }
});

app.get(['/api/config', '/api/config/:dashboard'], (req, res) => {
    const dashName = req.params.dashboard || 'default';
    const filePath = getFilePath(dashName);

    res.set('Cache-Control', 'no-store, no-cache, must-revalidate, private');

    if (!fs.existsSync(filePath)) {
        if (dashName === 'default' && fs.existsSync(path.join(DATA_DIR, 'config.json'))) {
             try {
                const raw = fs.readFileSync(path.join(DATA_DIR, 'config.json'), 'utf8');
                return res.json(JSON.parse(raw));
             } catch(e) {}
        }
        return res.status(404).json({ error: "Dashboard not found" });
    }

    try {
        const raw = fs.readFileSync(filePath, 'utf8');
        let config = {};
        try { config = JSON.parse(raw); } catch(e) { 
            return res.status(500).json({ error: "JSON Parse Error" }); 
        }

        if (!checkAuth(req, res, config, dashName)) return;

        if (config.settings) {
            if (config.settings.auth) delete config.settings.auth;
            if (config.settings.masterPassword) delete config.settings.masterPassword;
        }

        res.json(config);
    } catch (e) { 
        res.status(500).json({ error: "Server Error" }); 
    }
});

app.post(['/api/config', '/api/config/:dashboard'], (req, res) => {
    const dashName = req.params.dashboard || 'default';
    const filePath = getFilePath(dashName);
    
    let currentConfig = {};
    if (fs.existsSync(filePath)) {
        try {
            currentConfig = JSON.parse(fs.readFileSync(filePath, 'utf8'));
            if (!checkAuth(req, res, currentConfig, dashName)) return;
        } catch (e) { return res.status(500).json({ error: "Server Error" }); }
    } else {
        currentConfig = { settings: { auth: { username: "admin", password: "123" } } };
    }

    try {
        const newConfig = req.body;
        if (!newConfig.settings) newConfig.settings = {};

        if (currentConfig.settings?.masterPassword) newConfig.settings.masterPassword = currentConfig.settings.masterPassword;
        if (currentConfig.settings?.auth) newConfig.settings.auth = currentConfig.settings.auth;
        else if (dashName !== 'default' && !newConfig.settings.auth) newConfig.settings.auth = { username: "admin", password: "123" };
        
        if (!newConfig.settings.telegramSession && currentConfig.settings?.telegramSession) {
             newConfig.settings.telegramSession = currentConfig.settings.telegramSession;
        }

        fs.writeFileSync(filePath, JSON.stringify(newConfig, null, 2));
        
        if (activeTgClients.has(dashName)) {
            try {
                activeTgClients.get(dashName).disconnect();
                activeTgClients.delete(dashName);
                tgCache.delete(dashName); // Чистим кеш при обновлении конфига
            } catch(e) {}
        }

        res.json({ success: true });
    } catch (e) { res.status(500).json({ error: "Save failed" }); }
});

app.get(['/api/events', '/api/events/:dashboard'], async (req, res) => {
    const dashName = req.params.dashboard || 'default';
    const filePath = getFilePath(dashName);

    if (!fs.existsSync(filePath)) return res.json([]);

    try {
        const raw = fs.readFileSync(filePath, 'utf8');
        let config = {};
        try { config = JSON.parse(raw); } catch(e) { return res.json([]); }

        if (!checkAuth(req, res, config, dashName)) return;

        let allEvents = (config.events || []).map(e => ({ ...e, date: new Date(e.date), source: 'Manual' }));

        if (config.settings?.calendars && Array.isArray(config.settings.calendars)) {
            const promises = config.settings.calendars.map(async (cal) => {
                if (!cal.url) return [];
                try {
                    const response = await axios.get(cal.url, {
                        httpsAgent: agent, 
                        responseType: 'text',
                        timeout: 30000, // Увеличен таймаут до 30 сек
                        headers: { 'User-Agent': 'Mozilla/5.0 (NodeHomeLab)' }
                    });

                    const data = ical.parseICS(response.data);
                    const now = new Date();
                    const nextYear = new Date();
                    nextYear.setFullYear(now.getFullYear() + 1);
                    
                    const items = [];
                    for (const k in data) {
                        const ev = data[k];
                        if (ev.type === 'VEVENT') {
                            const icon = cal.icon || '📅';
                            const source = cal.name || 'Calendar';
                            if (ev.rrule && typeof ev.rrule.between === 'function') {
                                try {
                                    ev.rrule.between(now, nextYear).forEach(d => items.push({ name: ev.summary, date: d, icon, source, isCalendar: true }));
                                } catch (e) {}
                            } else if (ev.start) {
                                const date = new Date(ev.start);
                                if (date >= now && date <= nextYear) {
                                    items.push({ name: ev.summary, date: date, icon, source, isCalendar: true });
                                }
                            }
                        }
                    }
                    return items;
                } catch (err) {
                    console.error(`Calendar Error:`, err.message);
                    return [];
                }
            });
            const results = await Promise.all(promises);
            results.forEach(arr => allEvents = [...allEvents, ...arr]);
        }
        allEvents.sort((a, b) => a.date - b.date);
        res.json(allEvents.slice(0, 40)); 
    } catch (e) { res.status(500).json({ error: "Events Error" }); }
});

app.get(['/api/telegram', '/api/telegram/:dashboard'], async (req, res) => {
    const dashName = req.params.dashboard || 'default';
    const filePath = getFilePath(dashName);

    res.set('Cache-Control', 'no-store, no-cache, must-revalidate, private');

    if (!fs.existsSync(filePath)) return res.json({ error: "No config" });
    
    try {
        // Проверяем КЕШ
        const cached = tgCache.get(dashName);
        if (cached && (Date.now() - cached.timestamp < TG_CACHE_TTL)) {
            // Если кеш свежий (меньше 15 сек) - отдаем его
            return res.json(cached.data);
        }

        const raw = fs.readFileSync(filePath, 'utf8');
        let config = {};
        try { config = JSON.parse(raw); } catch(e) { return res.json({ error: "Config JSON Error" }); }

        if (!checkAuth(req, res, config, dashName)) return;

        const sessionString = config.settings?.telegramSession;
        if (!sessionString) return res.json({ notConfigured: true });

        const client = await getTgClient(dashName, sessionString);
        if (!client) return res.status(500).json({ error: "Connection failed" });

        // Реальный запрос в телеграм
        const dialogs = await client.getDialogs({ limit: 20 });
        const unreadChats = [];
        
        for (const d of dialogs) {
            // ТОЛЬКО непрочитанные
            if (d.unreadCount > 0) {
                unreadChats.push({
                    id: d.id.toString(),
                    msgId: d.message ? d.message.id : 0,
                    name: d.title || 'Unknown',
                    count: d.unreadCount,
                    message: d.message ? (d.message.message || '[Media]') : '',
                    date: d.date * 1000 
                });
            }
        }

        // Сохраняем в кеш
        tgCache.set(dashName, { data: unreadChats, timestamp: Date.now() });

        res.json(unreadChats);
    } catch (e) {
        console.error("TG API Error:", e.message);
        
        // Если ошибка FloodWait, берем старый кеш, если есть
        if (e.message && e.message.includes('flood') && tgCache.has(dashName)) {
             return res.json(tgCache.get(dashName).data);
        }
        
        res.status(500).json({ error: "TG Error" });
    }
});

app.get('/api/whoami', (req, res) => {
    let ip = req.headers['x-forwarded-for'] || req.socket.remoteAddress;
    if (ip && ip.startsWith('::ffff:')) ip = ip.replace('::ffff:', '');
    if (ip === '::1') ip = '127.0.0.1';
    const isLocal = (ip === '127.0.0.1' || ip.startsWith('192.168.') || ip.startsWith('10.'));
    res.json({ ip, isLocal });
});

process.on('unhandledRejection', (reason, promise) => {
    if (reason && reason.message && reason.message.includes('TIMEOUT')) return; 
    console.error('Unhandled Rejection:', reason);
});
process.on('uncaughtException', (err) => {
    if (err.message && err.message.includes('TIMEOUT')) return;
    console.error('Uncaught Exception:', err);
});

app.use(express.static(path.join(__dirname, 'dist')));
app.get(/.*/, (req, res) => res.sendFile(path.join(__dirname, 'dist', 'index.html')));

app.listen(PORT, () => {
    console.log(`Server running at http://localhost:${PORT}`);
});