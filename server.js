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

// --- 1. SETUP ---
if (dns.setDefaultResultOrder) {
    try { dns.setDefaultResultOrder('ipv4first'); } catch(e) {}
}

const agent = new https.Agent({ family: 4, keepAlive: true, timeout: 30000 });
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const DATA_DIR = path.join(__dirname, 'data');

if (!fs.existsSync(DATA_DIR)) {
    try { fs.mkdirSync(DATA_DIR); } catch(e) {}
}

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());
app.set('trust proxy', true);

// --- 2. TELEGRAM CLIENT SETUP ---
const TG_API_ID = 2040; 
const TG_API_HASH = "b18441a1ff607e10a989891a5462e627";
const tgCache = new Map();
const TG_CACHE_TTL = 30000;
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
const getFilePath = (name) => path.join(DATA_DIR, `${(name || 'default').replace(/[^a-z0-9-_]/gi, '').toLowerCase()}.json`);

// Единая функция авторизации
const checkAuth = (req, res, dashName) => {
    const safeName = (dashName || 'default').toLowerCase();
    
    // GET к дефолтному - без пароля
    if (safeName === 'default' && req.method === 'GET') return true;

    // Читаем конфиг, чтобы узнать пароль (сама функция читает файл)
    let config = {};
    const configPath = getFilePath(safeName);
    if (fs.existsSync(configPath)) {
        try { config = JSON.parse(fs.readFileSync(configPath, 'utf8')); } catch(e) {}
    } else {
        config = { settings: { auth: { username: "admin", password: "123" } } };
    }

    const authHeader = req.headers.authorization;
    if (!authHeader) {
        res.set('WWW-Authenticate', `Basic realm="Dashboard"`);
        res.status(401).send('Auth Required');
        return false;
    }

    const [login, password] = Buffer.from(authHeader.split(' ')[1], 'base64').toString().split(':');
    let validUser = 'admin';
    let validPass = 'password';
    
    if (safeName === 'default') {
        validPass = config.settings?.masterPassword || 'admin';
    } else {
        validUser = config.settings?.auth?.username || 'admin';
        validPass = config.settings?.auth?.password || 'password';
    }

    if (login !== validUser || password !== validPass) {
        res.set('WWW-Authenticate', `Basic realm="Dashboard"`);
        res.status(401).send('Access Denied');
        return false;
    }
    return true;
};

// --- 4. API DASHBOARDS & CONFIG ---
app.get('/api/dashboards', (req, res) => {
    try {
        const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json') && !f.startsWith('notes_') && !f.startsWith('telegram_'));
        const names = files.map(f => f.replace('.json', ''));
        if (!names.includes('default')) names.unshift('default');
        res.json([...new Set(names)]);
    } catch (e) { res.json(['default']); }
});

app.get('/api/config/:dashboard', (req, res) => {
    const dashName = req.params.dashboard || 'default';
    if (!checkAuth(req, res, dashName)) return;

    const filePath = getFilePath(dashName);
    if (!fs.existsSync(filePath)) {
        if (dashName === 'default') return res.json({ settings: { title: "DEFAULT" }, categories: [], events: [] });
        return res.status(404).json({ error: "Not found" });
    }
    try {
        const config = JSON.parse(fs.readFileSync(filePath, 'utf8'));
        const safeConfig = JSON.parse(JSON.stringify(config));
        if (safeConfig.settings) { 
            delete safeConfig.settings.auth; 
            delete safeConfig.settings.masterPassword; 
            delete safeConfig.settings.telegramSession; 
        }
        res.json(safeConfig);
    } catch (e) { res.status(500).json({ error: "Error" }); }
});

app.post('/api/config/:dashboard', (req, res) => {
    const dashName = req.params.dashboard || 'default';
    if (!checkAuth(req, res, dashName)) return;

    const filePath = getFilePath(dashName);
    let oldConfig = {};
    if (fs.existsSync(filePath)) try { oldConfig = JSON.parse(fs.readFileSync(filePath, 'utf8')); } catch(e) {}

    const newConfig = req.body;
    if (!newConfig.settings) newConfig.settings = {};
    
    if (oldConfig.settings?.auth) newConfig.settings.auth = oldConfig.settings.auth;
    if (oldConfig.settings?.masterPassword) newConfig.settings.masterPassword = oldConfig.settings.masterPassword;
    if (oldConfig.settings?.telegramSession && !newConfig.settings.telegramSession) {
        newConfig.settings.telegramSession = oldConfig.settings.telegramSession;
    }

    fs.writeFileSync(filePath, JSON.stringify(newConfig, null, 2));
    tgCache.delete(dashName);
    res.json({ success: true });
});

// --- 5. API NOTES ---
app.get('/api/notes/:dashboard', (req, res) => {
    const dashName = req.params.dashboard || 'default';
    if (!checkAuth(req, res, dashName)) return;

    const safeName = dashName.replace(/[^a-z0-9-_]/gi, '').toLowerCase();
    const notesPath = path.join(DATA_DIR, `notes_${safeName}.json`);

    if (fs.existsSync(notesPath)) {
        res.json(JSON.parse(fs.readFileSync(notesPath, 'utf8')));
    } else {
        res.json([]);
    }
});

app.post('/api/notes/:dashboard', (req, res) => {
    const dashName = req.params.dashboard || 'default';
    if (!checkAuth(req, res, dashName)) return;

    const safeName = dashName.replace(/[^a-z0-9-_]/gi, '').toLowerCase();
    const notesPath = path.join(DATA_DIR, `notes_${safeName}.json`);

    fs.writeFileSync(notesPath, JSON.stringify(req.body, null, 2));
    res.json({ success: true });
});

// --- 6. API EVENTS ---
app.get('/api/events/:dashboard', async (req, res) => {
    const dashName = req.params.dashboard || 'default';
    if (!checkAuth(req, res, dashName)) return;

    const filePath = getFilePath(dashName);
    if (!fs.existsSync(filePath)) return res.json([]);

    try {
        const config = JSON.parse(fs.readFileSync(filePath, 'utf8'));
        let allEvents = (config.events || []).map(e => ({ ...e, date: new Date(e.date), source: 'Manual' }));

        if (config.settings?.calendars) {
            const promises = config.settings.calendars.map(async (cal) => {
                if (!cal.url) return [];
                try {
                    const resp = await axios.get(cal.url, { httpsAgent: agent, timeout: 20000 });
                    const data = ical.parseICS(resp.data);
                    const now = new Date();
                    const nextYear = new Date(); nextYear.setFullYear(now.getFullYear() + 1);
                    const items = [];
                    for (const k in data) {
                        const ev = data[k];
                        if (ev.type === 'VEVENT') {
                            const obj = (d) => ({
                                name: ev.summary, date: d, source: 'Auto',
                                location: ev.location || '', description: ev.description || '', url: ev.url || '',
                                dtstart: ev.start
                            });
                            if (ev.rrule && ev.rrule.between) {
                                ev.rrule.between(now, nextYear).forEach(d => items.push(obj(d)));
                            } else if (ev.start && new Date(ev.start) >= now) {
                                items.push(obj(new Date(ev.start)));
                            }
                        }
                    }
                    return items;
                } catch(e) { return []; }
            });
            const results = await Promise.all(promises);
            results.forEach(r => allEvents.push(...r));
        }
        res.json(allEvents);
    } catch(e) { res.json([]); }
});

// --- 7. API TELEGRAM ---
app.get(['/api/telegram', '/api/telegram/:dashboard'], async (req, res) => {
    const dashName = req.params.dashboard || 'default';
    const filePath = getFilePath(dashName);
    
    // 1. Проверка файла
    if (!fs.existsSync(filePath)) return res.status(404).json({ error: "Config file not found" });
    
    try {
        // 2. Кеш
        const cached = tgCache.get(dashName);
        if (cached && (Date.now() - cached.timestamp < TG_CACHE_TTL)) {
            return res.json(cached.data);
        }

        // 3. Авторизация
        // ИСПРАВЛЕНИЕ: Передаем только 3 аргумента
        if (!checkAuth(req, res, dashName)) return;

        // 4. Читаем конфиг для сессии
        const raw = fs.readFileSync(filePath, 'utf8');
        const config = JSON.parse(raw);
        const sessionString = config.settings?.telegramSession;
        
        if (!sessionString) {
            return res.json({ notConfigured: true });
        }

        // 5. Коннект
        const client = await getTgClient(dashName, sessionString);
        if (!client) {
            return res.status(500).json({ error: "Client init failed" });
        }

        if (!await client.checkAuthorization()) {
             return res.status(401).json({ error: "Session expired" });
        }

        const dialogs = await client.getDialogs({ limit: 15 });
        const unreadChats = [];
        for (const d of dialogs) {
            if (d.unreadCount > 0) {
                unreadChats.push({
                    id: d.id.toString(),
                    msgId: d.message?.id || 0,
                    name: d.title || 'Unknown',
                    count: d.unreadCount,
                    message: d.message?.message || '[Media]',
                    date: (d.date || Date.now() / 1000) * 1000 
                });
            }
        }
        
        tgCache.set(dashName, { data: unreadChats, timestamp: Date.now() });
        res.json(unreadChats);

    } catch (e) {
        console.error("TG API Error:", e.message);
        if (e.message?.includes('flood') && tgCache.has(dashName)) {
             return res.json(tgCache.get(dashName).data);
        }
        res.status(500).json({ error: `TG Error: ${e.message}` });
    }
});

// --- 8. WHOAMI & STATIC ---
app.get('/api/whoami', (req, res) => {
    let ip = req.ip || req.socket.remoteAddress;
    if (ip && ip.includes('::ffff:')) ip = ip.replace('::ffff:', '');
    if (ip === '::1') ip = '127.0.0.1';
    res.json({ ip, isLocal: (ip === '127.0.0.1' || ip.startsWith('192.168.') || ip.startsWith('10.')) });
});

app.use(express.static(path.join(__dirname, 'dist')));

app.get(/.*/, (req, res) => {
    const index = path.join(__dirname, 'dist', 'index.html');
    if (fs.existsSync(index)) res.sendFile(index);
    else res.status(404).send('Frontend not built. Run "npm run build"');
});

app.listen(PORT, () => console.log(`Server running at http://localhost:${PORT}`));