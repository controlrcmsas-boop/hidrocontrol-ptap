-- HIDROCONTROL PTAP V2.0 - Estado de notificacion por correo para alarmas criticas

\c hidrocontrol_ptap

ALTER TABLE alarm_events
ADD COLUMN IF NOT EXISTS notification_email_sent_at TIMESTAMPTZ NULL;

GRANT SELECT, UPDATE ON alarm_events TO hidrocontrol_app;

