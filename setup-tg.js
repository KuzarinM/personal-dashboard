import { TelegramClient } from "telegram";
import { StringSession } from "telegram/sessions/index.js";
import input from "input";

// Используем публичные ключи, как договаривались
const apiId = 2040;
const apiHash = "b18441a1ff607e10a989891a5462e627";

(async () => {
  console.log("=== TELEGRAM SESSION GENERATOR ===");
  
  const client = new TelegramClient(new StringSession(""), apiId, apiHash, {
    connectionRetries: 5,
  });

  await client.start({
    phoneNumber: async () => await input.text("Номер телефона (+7...): "),
    password: async () => await input.text("Пароль 2FA (если есть): "),
    phoneCode: async () => await input.text("Код подтверждения: "),
    onError: (err) => console.log(err),
  });

  console.log("\n------------------------------------------------------------");
  console.log("УСПЕХ! Скопируй строку ниже (включая кавычки, если будут) и вставь в конфиг дашборда:");
  console.log("Поле: settings.telegramSession");
  console.log("------------------------------------------------------------\n");
  
  console.log(client.session.save()); // <--- ВОТ ЭТА СТРОКА НАМ НУЖНА
  
  console.log("\n------------------------------------------------------------");
  process.exit(0);
})();