-- HIDROCONTROL PTAP - Usuario de aplicacion Node-RED
-- Ejecutar conectado a la base hidrocontrol_ptap como postgres.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hidrocontrol_app') THEN
        CREATE ROLE hidrocontrol_app LOGIN PASSWORD 'hidrocontrol2026';
    ELSE
        ALTER ROLE hidrocontrol_app WITH LOGIN PASSWORD 'hidrocontrol2026';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE hidrocontrol_ptap TO hidrocontrol_app;
GRANT USAGE ON SCHEMA public TO hidrocontrol_app;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO hidrocontrol_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO hidrocontrol_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO hidrocontrol_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO hidrocontrol_app;
