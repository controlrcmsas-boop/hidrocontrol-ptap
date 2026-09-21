-- ==============================================================================
-- HIDROCONTROL PTAP V2.2 - MIGRACIÓN DDL DE REMEDIACIÓN Y COMPATIBILIDAD (M3)
-- Archivo: database/02-remediation-schema-m3.sql
-- Ejecutar contra la base de datos: 'hidrocontrol_ptap'
--
-- Objetivos:
-- 1. Sembrado idempotente de etiquetas centinela ('comunicacion_plc_s7', 'plc_conectado')
--    en la tabla 'tags' para resolver violaciones de integridad referencial (SQLSTATE 23503)
--    en telemetry_history y quality_history.
-- 2. Adición segura e idempotente de columnas 'acknowledged_by' y 'note' a 'alarm_events'
--    conforme al estándar ISA-18.2 para soporte de acuse de alarmas y compatibilidad
--    con modelos C# Blazor (AlarmEvent.cs).
-- 3. Sincronización de registros preexistentes entre 'operator_name' y 'acknowledged_by'.
-- 4. Creación de índice compuesto acelerador para consultas de estado de acuse.
-- ==============================================================================

BEGIN;

-- 1. Inserción / Actualización Idempotente de Etiquetas Centinela de Comunicación S7
INSERT INTO tags (
    tag_id, 
    nombre_visible, 
    abreviado, 
    clase, 
    tipo_senal, 
    db_number, 
    address, 
    tipo_dato_s7, 
    unidad, 
    lectura_escritura, 
    historizar, 
    alarma_habilitada, 
    min_value, 
    max_value, 
    descripcion
) VALUES 
(
    'comunicacion_plc_s7', 
    'Estado Enlace Comunicación PLC S7', 
    'S7_COMM', 
    'sistema', 
    'digital', 
    10, 
    'STATUS', 
    'REAL', 
    'status', 
    'lectura', 
    true, 
    true, 
    0, 
    1, 
    'Etiqueta centinela para auditoría de continuidad de comunicación con autómata S7-1200'
),
(
    'plc_conectado', 
    'PLC Conectado y Operativo', 
    'PLC_CON', 
    'sistema', 
    'digital', 
    10, 
    'STATUS', 
    'BOOL', 
    NULL, 
    'lectura', 
    true, 
    true, 
    0, 
    1, 
    'Bandera booleana de estado de enlace físico con autómata S7-1200'
)
ON CONFLICT (tag_id) DO UPDATE SET
    nombre_visible = EXCLUDED.nombre_visible,
    abreviado = EXCLUDED.abreviado,
    clase = EXCLUDED.clase,
    tipo_senal = EXCLUDED.tipo_senal,
    db_number = EXCLUDED.db_number,
    address = EXCLUDED.address,
    tipo_dato_s7 = EXCLUDED.tipo_dato_s7,
    unidad = EXCLUDED.unidad,
    lectura_escritura = EXCLUDED.lectura_escritura,
    historizar = EXCLUDED.historizar,
    alarma_habilitada = EXCLUDED.alarma_habilitada,
    min_value = EXCLUDED.min_value,
    max_value = EXCLUDED.max_value,
    descripcion = EXCLUDED.descripcion,
    updated_at = now();

-- 2. Adaptación de Esquema de Alarmas (ISA-18.2)
-- Adición condicional de columnas para compatibilidad de acuse y trazabilidad de operador
ALTER TABLE alarm_events ADD COLUMN IF NOT EXISTS acknowledged_by TEXT;
ALTER TABLE alarm_events ADD COLUMN IF NOT EXISTS note TEXT;

-- 3. Sincronización Idempotente de Datos Históricos
UPDATE alarm_events 
SET acknowledged_by = operator_name 
WHERE acknowledged_by IS NULL AND operator_name IS NOT NULL;

-- 4. Creación de Índice para Acelerar Búsquedas de Alarmas Reconocidas y Pendientes
CREATE INDEX IF NOT EXISTS idx_alarm_events_ack_state 
    ON alarm_events(state, acknowledged_at DESC);

COMMIT;

-- 5. Consultas de Verificación de Integridad Relacional
SELECT tag_id, nombre_visible, clase, tipo_senal, updated_at 
FROM tags 
WHERE tag_id IN ('comunicacion_plc_s7', 'plc_conectado');

SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'alarm_events' 
  AND column_name IN ('operator_name', 'acknowledged_by', 'note');
