-- Table document_templates : modèles de document imprimables (voir
-- src/Api/Features/DocumentTemplates et src/Web/Features/Documents).

CREATE TABLE `document_templates` (
  `IDDOCUMENT_TEMPLATES` BIGINT NOT NULL AUTO_INCREMENT,
  `Nom` VARCHAR(150) NOT NULL,
  `Type_document` VARCHAR(50) NOT NULL,
  `Description` VARCHAR(500) NULL,
  `Contenu_html` LONGTEXT NOT NULL,
  `IDUTILISATEURS_createur` BIGINT NULL DEFAULT 0,
  `IDUTILISATEURS_modificateur` BIGINT NULL DEFAULT 0,
  `Date_heure_creation` TIMESTAMP(0) NULL,
  `Date_heure_modification` TIMESTAMP(0) NULL,
  PRIMARY KEY (`IDDOCUMENT_TEMPLATES`),
  INDEX `WDIDX_DOCUMENT_TEMPLATES_Type_document` (`Type_document`),
  INDEX `document_templates_IDUTILISATEURS_createur_fkey` (`IDUTILISATEURS_createur`),
  INDEX `document_templates_IDUTILISATEURS_modificateur_fkey` (`IDUTILISATEURS_modificateur`),
  CONSTRAINT `document_templates_IDUTILISATEURS_createur_fkey` FOREIGN KEY (`IDUTILISATEURS_createur`) REFERENCES `utilisateurs` (`IDUTILISATEURS`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `document_templates_IDUTILISATEURS_modificateur_fkey` FOREIGN KEY (`IDUTILISATEURS_modificateur`) REFERENCES `utilisateurs` (`IDUTILISATEURS`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
