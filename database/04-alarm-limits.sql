-- HIDROCONTROL PTAP - Limites configurables de alarmas

CREATE TABLE IF NOT EXISTS alarm_limits (
    id BIGSERIAL PRIMARY KEY,
    tag_id TEXT NOT NULL REFERENCES tags(tag_id),
    alarm_type TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true,
    severity TEXT NOT NULL DEFAULT 'medium',
    limit_low NUMERIC(14, 4),
    limit_high NUMERIC(14, 4),
    bool_alarm_value BOOLEAN,
    unit TEXT,
    message TEXT NOT NULL,
    technical_note TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_alarm_limits_tag_type UNIQUE (tag_id, alarm_type)
);

CREATE INDEX IF NOT EXISTS idx_alarm_limits_enabled
    ON alarm_limits(enabled, tag_id);

INSERT INTO tags (tag_id, nombre_visible, abreviado, clase, tipo_senal, db_number, address, tipo_dato_s7, unidad, lectura_escritura, historizar, alarma_habilitada, min_value, max_value, descripcion)
VALUES
('cloro_residual', 'Cloro residual agua tratada', 'S11', 'calidad', 'analogica', NULL, NULL, 'REAL', 'mg/L', 'lectura', true, true, 0, 5, 'Variable preparada para sensor futuro de cloro residual')
ON CONFLICT (tag_id) DO UPDATE SET
    nombre_visible = EXCLUDED.nombre_visible,
    abreviado = EXCLUDED.abreviado,
    clase = EXCLUDED.clase,
    tipo_senal = EXCLUDED.tipo_senal,
    unidad = EXCLUDED.unidad,
    lectura_escritura = EXCLUDED.lectura_escritura,
    historizar = EXCLUDED.historizar,
    alarma_habilitada = EXCLUDED.alarma_habilitada,
    min_value = EXCLUDED.min_value,
    max_value = EXCLUDED.max_value,
    descripcion = EXCLUDED.descripcion,
    updated_at = now();

INSERT INTO alarm_limits (tag_id, alarm_type, enabled, severity, limit_low, limit_high, bool_alarm_value, unit, message, technical_note)
VALUES
('ph_agua_cruda', 'range', true, 'high', 6.5, 8.5, NULL, 'pH', 'pH de agua cruda fuera de limite', 'Limites iniciales editables desde Configuracion'),
('ph_agua_tratada', 'range', true, 'high', 6.5, 8.5, NULL, 'pH', 'pH de agua tratada fuera de limite', 'Limites iniciales editables desde Configuracion'),
('turbidez_agua_cruda', 'high', true, 'medium', NULL, 20.0, NULL, 'NTU', 'Turbidez de agua cruda alta', 'Ajustar segun criterio de proceso'),
('turbidez_agua_tratada', 'high', true, 'high', NULL, 5.0, NULL, 'NTU', 'Turbidez de agua tratada alta', 'Ajustar segun norma/criterio del demo'),
('cloro_residual', 'range', false, 'high', 0.3, 2.0, NULL, 'mg/L', 'Cloro residual fuera de limite', 'Preparado para cuando se agregue el sensor de cloro residual'),
('bomba_principal_estado', 'bool_equals', true, 'medium', NULL, NULL, false, NULL, 'Bomba principal detenida', 'Dispara cuando el estado real de B1 es FALSE'),
('nivel_tanque_agua_cruda', 'bool_equals', true, 'medium', NULL, NULL, true, NULL, 'Tanque de agua cruda lleno', 'Dispara cuando el sensor digital S1 esta TRUE')
ON CONFLICT (tag_id, alarm_type) DO UPDATE SET
    enabled = EXCLUDED.enabled,
    severity = EXCLUDED.severity,
    limit_low = EXCLUDED.limit_low,
    limit_high = EXCLUDED.limit_high,
    bool_alarm_value = EXCLUDED.bool_alarm_value,
    unit = EXCLUDED.unit,
    message = EXCLUDED.message,
    technical_note = EXCLUDED.technical_note,
    updated_at = now();

GRANT SELECT, INSERT, UPDATE ON alarm_limits TO hidrocontrol_app;
GRANT USAGE, SELECT ON SEQUENCE alarm_limits_id_seq TO hidrocontrol_app;
