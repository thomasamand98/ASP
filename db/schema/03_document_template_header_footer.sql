-- en-tête et pied de page des modèles

ALTER TABLE `document_templates`
  ADD COLUMN `Contenu_entete` LONGTEXT NULL AFTER `Description`,
  ADD COLUMN `Contenu_pied` LONGTEXT NULL AFTER `Contenu_html`;
