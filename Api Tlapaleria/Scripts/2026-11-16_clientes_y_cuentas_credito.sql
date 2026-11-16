-- =================================================================================
-- Módulo de Clientes de Confianza y Cuentas por Cobrar (Ventas a Crédito / Fiado)
-- Ejecutar UNA vez en la base de datos MySQL (TlapaleriaLeo) ANTES de usar el módulo
-- =================================================================================

-- 1. Tabla de Clientes de Confianza
CREATE TABLE IF NOT EXISTS `Customers` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(150) NOT NULL,
    `Alias` VARCHAR(100) NULL,
    `Phone` VARCHAR(30) NULL,
    `SecondaryPhone` VARCHAR(30) NULL,
    `Email` VARCHAR(100) NULL,
    `Address` VARCHAR(255) NULL,
    `Notes` VARCHAR(500) NULL,
    `CreditLimit` DECIMAL(12,2) NULL,
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    INDEX `IX_Customers_Name` (`Name`),
    INDEX `IX_Customers_Phone` (`Phone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_spanish_ci;

-- 2. Relación de Ventas (Sales) con Clientes (CustomerId opcional para contado, obligatorio en crédito)
ALTER TABLE `Sales`
    ADD COLUMN `CustomerId` INT NULL,
    ADD INDEX `IX_Sales_CustomerId` (`CustomerId`),
    ADD CONSTRAINT `FK_Sales_Customers_CustomerId`
        FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE SET NULL;

-- 3. Desglose de Devoluciones (Returns) para distinguir cuánto descontó de deuda y cuánto salió en efectivo
ALTER TABLE `Returns`
    ADD COLUMN `AppliedToDebtAmount` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    ADD COLUMN `CashRefundedAmount`  DECIMAL(10,2) NOT NULL DEFAULT 0.00;

-- Inicializar las devoluciones históricas de contado con su monto reembolsado en efectivo
UPDATE `Returns`
SET `CashRefundedAmount` = `TotalRefunded`
WHERE `CashRefundedAmount` = 0 AND `AppliedToDebtAmount` = 0;

-- 4. Tabla de Cuentas de Crédito (Una cuenta por cada ticket vendido a crédito)
CREATE TABLE IF NOT EXISTS `CreditAccounts` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Folio` VARCHAR(50) NOT NULL,
    `CustomerId` INT NOT NULL,
    `SaleId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `OriginalAmount` DECIMAL(10,2) NOT NULL,
    `ReturnedAmount` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    `PaidAmount` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    `Balance` DECIMAL(10,2) NOT NULL,
    `Status` VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    `DueDate` DATETIME NULL,
    `Notes` VARCHAR(255) NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `PaidAt` DATETIME NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_CreditAccounts_Folio` (`Folio`),
    UNIQUE INDEX `IX_CreditAccounts_SaleId` (`SaleId`),
    INDEX `IX_CreditAccounts_CustomerId_Status` (`CustomerId`, `Status`),
    CONSTRAINT `FK_CreditAccounts_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CreditAccounts_Sales_SaleId` FOREIGN KEY (`SaleId`) REFERENCES `Sales` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CreditAccounts_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_spanish_ci;

-- 5. Tabla de Abonos / Pagos a Cuentas de Crédito
CREATE TABLE IF NOT EXISTS `CreditPayments` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `PaymentFolio` VARCHAR(50) NOT NULL,
    `CreditAccountId` INT NOT NULL,
    `CustomerId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `Amount` DECIMAL(10,2) NOT NULL,
    `PaymentMethod` VARCHAR(50) NOT NULL DEFAULT 'Efectivo',
    `PaymentReference` VARCHAR(100) NULL,
    `Notes` VARCHAR(255) NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_CreditPayments_PaymentFolio` (`PaymentFolio`),
    INDEX `IX_CreditPayments_CreditAccountId` (`CreditAccountId`),
    INDEX `IX_CreditPayments_CustomerId` (`CustomerId`),
    INDEX `IX_CreditPayments_CreatedAt` (`CreatedAt`),
    CONSTRAINT `FK_CreditPayments_CreditAccounts_CreditAccountId` FOREIGN KEY (`CreditAccountId`) REFERENCES `CreditAccounts` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CreditPayments_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CreditPayments_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_spanish_ci;

-- 6. Nuevos Permisos del Módulo de Clientes y Cuentas de Crédito
INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'view.customers', 'Permite ver el catálogo de clientes y su información de contacto'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'view.customers');

INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'add.customers', 'Permite registrar nuevos clientes de confianza'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'add.customers');

INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'edit.customers', 'Permite editar datos de contacto y límite de crédito de clientes'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'edit.customers');

INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'delete.customers', 'Permite desactivar clientes sin deuda pendiente'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'delete.customers');

INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'view.credit_accounts', 'Permite consultar las cuentas por cobrar, deudas y abonos de clientes'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'view.credit_accounts');

INSERT INTO `Permisos` (`NombreSistema`, `Descripcion`)
SELECT 'manage.credit_accounts', 'Permite otorgar ventas a crédito y registrar abonos de clientes'
WHERE NOT EXISTS (SELECT 1 FROM `Permisos` WHERE `NombreSistema` = 'manage.credit_accounts');
