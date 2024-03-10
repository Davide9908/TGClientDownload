INSERT INTO "AnimeEpisodesSetting" ("TelegramChannelId","FileNameTemplate")
SELECT "TelegramChatId", "FileNameTemplate"
FROM "TelegramChannel"
WHERE "FileNameTemplate" is not null