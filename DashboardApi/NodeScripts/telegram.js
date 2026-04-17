const { TelegramClient } = require("telegram");
const { StringSession } = require("telegram/sessions");
const { Api } = require("telegram/tl");

// Хранилище клиентов
const activeClients = new Map();

// Ваши ключи
const apiId = 2040;
const apiHash = "b18441a1ff607e10a989891a5462e627";

module.exports = async function (input) {
    const args = input || {};

    // ОТЛАДКА: Раскомментируйте, если снова упадет
    // console.log("[NodeJS] Input args:", JSON.stringify(args, null, 2));

    if (!args.action) {
        throw new Error("Error: 'action' argument is missing");
    }

    const { action, dashboardId, payload } = args;

    try {
        let result = null;

        switch (action) {
            case 'getMessages':
                result = await getMessages(dashboardId, payload?.session);
                break;
            case 'auth_start':
                // Добавил проверку
                if (!payload || !payload.phone) throw new Error("Phone is missing in payload");
                result = await authStart(dashboardId, payload.phone);
                break;
            case 'auth_complete':
                result = await authComplete(dashboardId, payload?.code, payload?.password);
                break;
            default:
                throw new Error(`Unknown action: ${action}`);
        }

        return result;

    } catch (error) {
        console.error(`[NodeJS Error] ${error.message}`);
        // Возвращаем ошибку авторизации как статус, чтобы фронт понял
        if (error.message.includes("PHONE_NUMBER_INVALID") || error.message.includes("wrong type")) {
            // Можно вернуть специфичный статус
        }
        throw error;
    }
};

// --- ИМПЛЕМЕНТАЦИЯ ---

async function getClient(dashboardId, sessionString) {
    if (activeClients.has(dashboardId)) {
        const client = activeClients.get(dashboardId);
        if (client.connected) return client;
    }

    // ВАЖНО: Защита от мусора в sessionString
    let session;
    try {
        // Если строка пустая или null - создаем пустую сессию
        session = new StringSession(sessionString || "");
    } catch (e) {
        // Если строка есть, но она "битая" (например, от старой либы), StringSession выкинет ошибку.
        // Мы пробрасываем её наверх, чтобы getMessages вернул notConfigured.
        throw new Error("Not a valid string");
    }

    const client = new TelegramClient(session, apiId, apiHash, {
        connectionRetries: 5,
        useWSS: false,
        useIPV6: false,
        timeout: 10000
    });

    // Подавляем ошибки подключения при инициализации
    // (например, если интернет упал, мы не хотим крашить всё приложение)
    try {
        await client.connect();
    } catch (e) {
        console.warn(`[JS] Connect warning: ${e.message}`);
    }

    activeClients.set(dashboardId, client);
    return client;
}

async function getMessages(id, session) {
    // Если сессии нет совсем - сразу выход
    if (!session) return { notConfigured: true };

    let client;
    try {
        client = await getClient(id, session);
    } catch (e) {
        // Ловим ошибку "Not a valid string" здесь
        if (e.message.includes("Not a valid string")) {
            console.warn(`[JS] Session for ${id} is corrupted. Resetting.`);
            return { notConfigured: true };
        }
        throw e;
    }

    // Проверка авторизации
    if (!await client.checkAuthorization()) {
        return { notConfigured: true };
    }

    const dialogs = await client.getDialogs({ limit: 15 });
    const result = [];

    for (const d of dialogs) {
        if (d.isChannel && !d.isGroup) continue;

        // Фильтрация
        if (d.unreadCount > 0 || d.unreadMentionsCount > 0) {
            result.push({
                id: d.id.toString(),
                name: d.title || 'Unknown',
                count: d.unreadCount,
                message: d.message?.message || '[Media]',
                date: d.date * 1000,
                isNew: d.unreadMentionsCount > 0
            });
        }
    }

    return result;
}

async function authStart(id, phone) {
    console.log(`[JS] Starting Auth for ${phone}`);

    // 1. Очистка старого клиента
    if (activeClients.has(id)) {
        const old = activeClients.get(id);
        await old.disconnect();
        activeClients.delete(id);
    }

    // 2. Создаем чистый клиент
    const client = await getClient(id, "");

    // 3. Низкоуровневый вызов (Bypass client.sendCode helper)
    try {
        // Принудительно чистим номер от пробелов и скобок
        const cleanPhone = String(phone).replace(/\s+/g, '').replace(/[()]/g, '');

        // Убедимся, что apiId это число
        const idInt = parseInt(apiId);

        console.log(`[JS] Invoking auth.SendCode for ${cleanPhone} (AppID: ${idInt})`);

        const result = await client.invoke(
            new Api.auth.SendCode({
                phoneNumber: cleanPhone,
                apiId: idInt,
                apiHash: String(apiHash),
                settings: new Api.CodeSettings({
                    allowFlashcall: false,
                    currentNumber: false,
                    allowAppHash: false
                })
            })
        );

        console.log("[JS] Code sent successfully. Hash:", result.phoneCodeHash);

        // Сохраняем хеш для следующего шага
        client._tempAuth = {
            phone: cleanPhone,
            phoneCodeHash: result.phoneCodeHash
        };

        return { status: 'CODE_SENT' };

    } catch (e) {
        console.error("[JS] Auth Start Error:", e);
        // Частая ошибка - если номер не зарегистрирован или забанен
        if (e.errorMessage === 'PHONE_NUMBER_INVALID') {
            throw new Error("Invalid phone number or format (use international format +123...)");
        }
        throw e;
    }
}

async function authComplete(id, code, password) {
    const client = activeClients.get(id);
    if (!client || !client._tempAuth) throw new Error("Auth flow not started");

    const { phone, phoneCodeHash } = client._tempAuth;

    try {
        await client.invoke(new Api.auth.SignIn({
            phoneNumber: phone,
            phoneCodeHash: phoneCodeHash,
            phoneCode: code
        }));
    } catch (e) {
        if (e.errorMessage === 'SESSION_PASSWORD_NEEDED') {
            if (!password) return { status: 'PASSWORD_NEEDED' };

            await client.signInWithPassword({
                apiId,
                apiHash,
                password: password,
                onError: (err) => { throw err }
            });
        } else {
            throw e;
        }
    }

    const sessionString = client.session.save();
    delete client._tempAuth;

    return { status: 'SUCCESS', session: sessionString };
}