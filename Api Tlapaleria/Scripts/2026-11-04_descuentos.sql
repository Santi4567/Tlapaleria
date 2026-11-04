-- Descuentos en ventas (clientes/familiares)
-- Ejecutar UNA vez en la base de datos MySQL ANTES de arrancar la API con estos cambios.

ALTER TABLE `Sales`
    ADD COLUMN `DiscountPercent` decimal(5,2) NOT NULL DEFAULT 0,
    ADD COLUMN `DiscountAmount`  decimal(10,2) NOT NULL DEFAULT 0;

ALTER TABLE `SaleDetails`
    ADD COLUMN `DiscountAmount` decimal(10,2) NOT NULL DEFAULT 0;

-- Nuevo permiso. Asignalo despues al rol correspondiente desde la pantalla de Roles.
-- (Admin siempre tiene acceso.) Ajusta el nombre de la tabla si en tu BD es distinto.
INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'apply.discounts', 'Permite aplicar descuentos al cobrar una venta'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'apply.discounts');
