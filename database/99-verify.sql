-- HIDROCONTROL PTAP - Verificacion rapida

SELECT 'tags' AS tabla, count(*) AS registros FROM tags
UNION ALL
SELECT 'quality_history', count(*) FROM quality_history
UNION ALL
SELECT 'telemetry_history', count(*) FROM telemetry_history
UNION ALL
SELECT 'command_log', count(*) FROM command_log
UNION ALL
SELECT 'alarm_events', count(*) FROM alarm_events
UNION ALL
SELECT 'notification_emails', count(*) FROM notification_emails;

SELECT tag_id, nombre_visible, db_number, address, tipo_dato_s7, unidad
FROM tags
ORDER BY db_number, id;

SELECT id, tag_id, severity, state, notification_email_sent_at
FROM alarm_events
WHERE severity = 'critical'
ORDER BY id DESC
LIMIT 20;
