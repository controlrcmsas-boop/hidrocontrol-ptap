-- HIDROCONTROL PTAP - Crear base de datos
-- Ejecutar como usuario postgres.

CREATE DATABASE hidrocontrol_ptap
    WITH
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Spanish_Colombia.1252'
    LC_CTYPE = 'Spanish_Colombia.1252'
    TEMPLATE = template0
    CONNECTION LIMIT = -1;
