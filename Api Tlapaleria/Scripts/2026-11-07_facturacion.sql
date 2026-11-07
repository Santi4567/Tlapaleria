-- Módulo de Facturación Electrónica (Facturapi / CFDI 4.0)
-- Ejecutar UNA vez en la base de datos MySQL (TlapaleriaLeo)

CREATE TABLE IF NOT EXISTS `Invoices` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `SaleId` INT NOT NULL,
    `FacturapiId` VARCHAR(50) NOT NULL,
    `Uuid` VARCHAR(50) NOT NULL,
    `Rfc` VARCHAR(20) NOT NULL,
    `RazonSocial` VARCHAR(250) NOT NULL,
    `RegimenFiscal` VARCHAR(10) NOT NULL,
    `CodigoPostal` VARCHAR(10) NOT NULL,
    `UsoCfdi` VARCHAR(10) NOT NULL,
    `Total` DECIMAL(10,2) NOT NULL,
    `Status` VARCHAR(20) NOT NULL DEFAULT 'valid',
    `VerificationUrl` VARCHAR(500) NULL,
    `CancelReason` VARCHAR(50) NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `CanceledAt` DATETIME NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_Invoices_SaleId` (`SaleId`),
    INDEX `IX_Invoices_Uuid` (`Uuid`),
    CONSTRAINT `FK_Invoices_Sales_SaleId` FOREIGN KEY (`SaleId`) REFERENCES `Sales` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_spanish_ci;

-- Permiso para facturar. (El Administrador siempre tiene acceso por defecto).
-- Asignar este permiso a los roles correspondientes (Vendedor / Cajero) si se desea.
INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'manage.invoices', 'Permite emitir, consultar y cancelar facturas electrónicas'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'manage.invoices');
