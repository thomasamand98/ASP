-- Ajoute l'en-tête et le pied de page (répétés sur chaque page à
-- l'impression) au modèle de document.

ALTER TABLE `document_templates`
  ADD COLUMN `Contenu_entete` LONGTEXT NULL AFTER `Description`,
  ADD COLUMN `Contenu_pied` LONGTEXT NULL AFTER `Contenu_html`;
