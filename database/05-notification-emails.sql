-- HIDROCONTROL PTAP V2.0 - Correos para alarmas criticas

\c hidrocontrol_ptap

CREATE TABLE IF NOT EXISTS notification_emails (
    id BIGSERIAL PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    name TEXT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

GRANT SELECT, INSERT, UPDATE ON notification_emails TO hidrocontrol_app;
GRANT USAGE, SELECT ON SEQUENCE notification_emails_id_seq TO hidrocontrol_app;

