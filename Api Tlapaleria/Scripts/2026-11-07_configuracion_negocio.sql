-- Configuración del Negocio y Facturación (Personalizable por cliente)
-- Ejecutar UNA vez en la base de datos MySQL (TlapaleriaLeo)

CREATE TABLE IF NOT EXISTS `BusinessConfigs` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `BusinessName` VARCHAR(150) NOT NULL DEFAULT 'Tlapalería y Materiales Leo',
    `Rfc` VARCHAR(20) NULL,
    `PostalCode` VARCHAR(10) NOT NULL DEFAULT '72310',
    `Address` VARCHAR(250) NULL,
    `Phone` VARCHAR(50) NULL,
    `Email` VARCHAR(150) NULL,
    `FacturacionActiva` TINYINT(1) NOT NULL DEFAULT 1,
    `FacturapiApiKey` VARCHAR(200) NULL,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_spanish_ci;

-- Fila inicial única si la tabla está vacía
INSERT INTO `BusinessConfigs` (`Id`, `BusinessName`, `PostalCode`, `FacturacionActiva`, `UpdatedAt`)
SELECT 1, 'Tlapalería y Materiales Leo', '72310', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `BusinessConfigs` WHERE `Id` = 1);
