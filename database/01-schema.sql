-- HIDROCONTROL PTAP - Esquema inicial PostgreSQL

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS tags (
    id BIGSERIAL PRIMARY KEY,
    tag_id TEXT NOT NULL UNIQUE,
    nombre_visible TEXT NOT NULL,
    abreviado TEXT,
    clase TEXT NOT NULL,
    tipo_senal TEXT NOT NULL,
    db_number INTEGER,
    address TEXT,
    tipo_dato_s7 TEXT,
    unidad TEXT,
    lectura_escritura TEXT NOT NULL DEFAULT 'lectura',
    historizar BOOLEAN NOT NULL DEFAULT false,
    alarma_habilitada BOOLEAN NOT NULL DEFAULT false,
    min_value NUMERIC(12, 4),
    max_value NUMERIC(12, 4),
    descripcion TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS telemetry_history (
    id BIGSERIAL PRIMARY KEY,
    tag_id TEXT NOT NULL REFERENCES tags(tag_id),
    value_numeric NUMERIC(14, 4),
    value_bool BOOLEAN,
    quality TEXT NOT NULL DEFAULT 'good',
    source TEXT NOT NULL DEFAULT 'node-red',
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_telemetry_history_tag_time
    ON telemetry_history(tag_id, recorded_at DESC);

CREATE TABLE IF NOT EXISTS quality_history (
    id BIGSERIAL PRIMARY KEY,
    tag_id TEXT NOT NULL REFERENCES tags(tag_id),
    value_numeric NUMERIC(14, 4) NOT NULL,
    unit TEXT,
    quality TEXT NOT NULL DEFAULT 'good',
    source TEXT NOT NULL DEFAULT 'node-red',
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_quality_history_tag_time
    ON quality_history(tag_id, recorded_at DESC);

CREATE TABLE IF NOT EXISTS command_log (
    id BIGSERIAL PRIMARY KEY,
    sequence INTEGER,
    target TEXT NOT NULL,
    command TEXT NOT NULL,
    plc_variable TEXT,
    operator_name TEXT NOT NULL DEFAULT 'unknown',
    source TEXT NOT NULL DEFAULT 'dashboard',
    status TEXT NOT NULL DEFAULT 'requested',
    request_payload JSONB,
    response_payload JSONB,
    requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    executed_at TIMESTAMPTZ,
    result_message TEXT
);

CREATE INDEX IF NOT EXISTS idx_command_log_time
    ON command_log(requested_at DESC);

CREATE TABLE IF NOT EXISTS alarm_events (
    id BIGSERIAL PRIMARY KEY,
    tag_id TEXT NOT NULL REFERENCES tags(tag_id),
    alarm_type TEXT NOT NULL,
    severity TEXT NOT NULL DEFAULT 'medium',
    value_numeric NUMERIC(14, 4),
    value_bool BOOLEAN,
    message TEXT NOT NULL,
    state TEXT NOT NULL DEFAULT 'active',
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    acknowledged_at TIMESTAMPTZ,
    cleared_at TIMESTAMPTZ,
    operator_name TEXT
);

CREATE INDEX IF NOT EXISTS idx_alarm_events_state_time
    ON alarm_events(state, started_at DESC);

CREATE TABLE IF NOT EXISTS equipment_events (
    id BIGSERIAL PRIMARY KEY,
    equipment_id TEXT NOT NULL,
    event_type TEXT NOT NULL,
    previous_state TEXT,
    new_state TEXT,
    source TEXT NOT NULL DEFAULT 'plc',
    message TEXT,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_equipment_events_time
    ON equipment_events(equipment_id, recorded_at DESC);

CREATE TABLE IF NOT EXISTS system_events (
    id BIGSERIAL PRIMARY KEY,
    event_type TEXT NOT NULL,
    component TEXT NOT NULL,
    severity TEXT NOT NULL DEFAULT 'info',
    message TEXT NOT NULL,
    payload JSONB,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_system_events_time
    ON system_events(recorded_at DESC);

CREATE OR REPLACE VIEW v_latest_quality AS
SELECT DISTINCT ON (tag_id)
    tag_id,
    value_numeric,
    unit,
    quality,
    recorded_at
FROM quality_history
ORDER BY tag_id, recorded_at DESC;

CREATE OR REPLACE VIEW v_latest_telemetry AS
SELECT DISTINCT ON (tag_id)
    tag_id,
    value_numeric,
    value_bool,
    quality,
    recorded_at
FROM telemetry_history
ORDER BY tag_id, recorded_at DESC;
