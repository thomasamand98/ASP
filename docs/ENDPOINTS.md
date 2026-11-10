# Endpoints : correspondance NestJS / ASP.NET Core

Liste sortie pendant la migration par `tools/list-endpoints.mjs` (le script
n'existe plus). Elle date d'avant le retrait des endpoints qu'aucun écran
n'appelait (`/auth/me`, `/clients`, `/fournisseurs`, etc.), donc elle n'est
plus à jour : pour la liste actuelle, voir `/swagger` avec l'API en dev.

117 endpoints à l'époque. Tout demande un jeton d'accès
(`Authorization: Bearer ...`) sauf `POST /auth/login`, `POST /auth/refresh` et
`POST /auth/logout`. Un `POST` renvoie 201 (200 pour les trois routes
`/auth`), un `DELETE` renvoie 200 sans corps.

La colonne « Vérifié » servait à la recette manuelle. La comparaison
automatique avec NestJS passait par `tools/compare.mjs` (lectures) et
`tools/compare-writes.mjs` (écritures), supprimés depuis.

| Vérifié | Verbe | Route | NestJS | ASP.NET Core |
|---|---|---|---|---|
| ☐ | `GET` | `/attelages` | `AttelageController.getAttelages` | `AttelageController.GetAttelages` |
| ☐ | `POST` | `/attelages` | `AttelageController.createAttelage` | `AttelageController.CreateAttelage` |
| ☐ | `DELETE` | `/attelages/:id` | `AttelageController.deleteAttelage` | `AttelageController.DeleteAttelage` |
| ☐ | `GET` | `/attelages/:id` | `AttelageController.getAttelage` | `AttelageController.GetAttelage` |
| ☐ | `PATCH` | `/attelages/:id` | `AttelageController.updateAttelage` | `AttelageController.UpdateAttelage` |
| ☐ | `POST` | `/auth/login` | `AuthController.login` | `AuthController.Login` |
| ☐ | `POST` | `/auth/logout` | `AuthController.logout` | `AuthController.Logout` |
| ☐ | `GET` | `/auth/me` | `AuthController.me` | `AuthController.Me` |
| ☐ | `POST` | `/auth/refresh` | `AuthController.refresh` | `AuthController.Refresh` |
| ☐ | `GET` | `/categories-enumeration` | `CategorieEnumerationController.getCategoriesEnumeration` | `CategorieEnumerationController.GetCategoriesEnumeration` |
| ☐ | `POST` | `/categories-enumeration` | `CategorieEnumerationController.createCategorieEnumeration` | `CategorieEnumerationController.CreateCategorieEnumeration` |
| ☐ | `DELETE` | `/categories-enumeration/:id` | `CategorieEnumerationController.deleteCategorieEnumeration` | `CategorieEnumerationController.DeleteCategorieEnumeration` |
| ☐ | `PATCH` | `/categories-enumeration/:id` | `CategorieEnumerationController.updateCategorieEnumeration` | `CategorieEnumerationController.UpdateCategorieEnumeration` |
| ☐ | `GET` | `/chauffeurs` | `ChauffeurController.getChauffeurs` | `ChauffeurController.GetChauffeurs` |
| ☐ | `POST` | `/chauffeurs` | `ChauffeurController.createChauffeur` | `ChauffeurController.CreateChauffeur` |
| ☐ | `DELETE` | `/chauffeurs/:id` | `ChauffeurController.deleteChauffeur` | `ChauffeurController.DeleteChauffeur` |
| ☐ | `GET` | `/chauffeurs/:id` | `ChauffeurController.getChauffeur` | `ChauffeurController.GetChauffeur` |
| ☐ | `PATCH` | `/chauffeurs/:id` | `ChauffeurController.updateChauffeur` | `ChauffeurController.UpdateChauffeur` |
| ☐ | `GET` | `/clients` | `ClientController.getClients` | `ReferentielsController.GetClients` |
| ☐ | `GET` | `/commandes` | `CommandeController.getCommandes` | `CommandeController.GetCommandes` |
| ☐ | `POST` | `/commandes` | `CommandeController.createCommande` | `CommandeController.CreateCommande` |
| ☐ | `DELETE` | `/commandes/:id` | `CommandeController.deleteCommande` | `CommandeController.DeleteCommande` |
| ☐ | `GET` | `/commandes/:id` | `CommandeController.getCommande` | `CommandeController.GetCommande` |
| ☐ | `PATCH` | `/commandes/:id` | `CommandeController.updateCommande` | `CommandeController.UpdateCommande` |
| ☐ | `GET` | `/conditions` | `ConditionController.getConditions` | `ConditionController.GetConditions` |
| ☐ | `POST` | `/conditions` | `ConditionController.createCondition` | `ConditionController.CreateCondition` |
| ☐ | `POST` | `/conditions-cmr` | `ConditionCmrController.createConditionCmr` | `ConditionCmrController.CreateConditionCmr` |
| ☐ | `DELETE` | `/conditions-cmr/:id` | `ConditionCmrController.deleteConditionCmr` | `ConditionCmrController.DeleteConditionCmr` |
| ☐ | `PATCH` | `/conditions-cmr/:id` | `ConditionCmrController.updateConditionCmr` | `ConditionCmrController.UpdateConditionCmr` |
| ☐ | `DELETE` | `/conditions/:id` | `ConditionController.deleteCondition` | `ConditionController.DeleteCondition` |
| ☐ | `GET` | `/conditions/:id` | `ConditionController.getCondition` | `ConditionController.GetCondition` |
| ☐ | `PATCH` | `/conditions/:id` | `ConditionController.updateCondition` | `ConditionController.UpdateCondition` |
| ☐ | `GET` | `/contacts` | `ContactController.getContacts` | `ContactController.GetContacts` |
| ☐ | `POST` | `/contacts` | `ContactController.createContact` | `ContactController.CreateContact` |
| ☐ | `DELETE` | `/contacts/:id` | `ContactController.deleteContact` | `ContactController.DeleteContact` |
| ☐ | `GET` | `/contacts/:id` | `ContactController.getContact` | `ContactController.GetContact` |
| ☐ | `PATCH` | `/contacts/:id` | `ContactController.updateContact` | `ContactController.UpdateContact` |
| ☐ | `GET` | `/contrats` | `ContratController.getContrats` | `ContratController.GetContrats` |
| ☐ | `POST` | `/contrats` | `ContratController.createContrat` | `ContratController.CreateContrat` |
| ☐ | `DELETE` | `/contrats-travail/:id` | `ContratTravailController.deleteContrat` | `ContratTravailController.DeleteContrat` |
| ☐ | `PATCH` | `/contrats-travail/:id` | `ContratTravailController.updateContrat` | `ContratTravailController.UpdateContrat` |
| ☐ | `DELETE` | `/contrats/:id` | `ContratController.deleteContrat` | `ContratController.DeleteContrat` |
| ☐ | `GET` | `/contrats/:id` | `ContratController.getContrat` | `ContratController.GetContrat` |
| ☐ | `PATCH` | `/contrats/:id` | `ContratController.updateContrat` | `ContratController.UpdateContrat` |
| ☐ | `GET` | `/contrats/:id/pdf` | `ContratController.getContratPdf` | `ContratController.GetContratPdf` |
| ☐ | `GET` | `/contrats/:id/prestations` | `ContratController.getContratPrestations` | `ContratController.GetContratPrestations` |
| ☐ | `GET` | `/dechets` | `DechetController.getDechets` | `ReferentielsController.GetDechets` |
| ☐ | `GET` | `/document-templates` | `DocumentTemplateController.getDocumentTemplates` | `DocumentTemplateController.GetDocumentTemplates` |
| ☐ | `POST` | `/document-templates` | `DocumentTemplateController.createDocumentTemplate` | `DocumentTemplateController.CreateDocumentTemplate` |
| ☐ | `DELETE` | `/document-templates/:id` | `DocumentTemplateController.deleteDocumentTemplate` | `DocumentTemplateController.DeleteDocumentTemplate` |
| ☐ | `GET` | `/document-templates/:id` | `DocumentTemplateController.getDocumentTemplate` | `DocumentTemplateController.GetDocumentTemplate` |
| ☐ | `PATCH` | `/document-templates/:id` | `DocumentTemplateController.updateDocumentTemplate` | `DocumentTemplateController.UpdateDocumentTemplate` |
| ☐ | `GET` | `/entite` | `EntiteController.getEntite` | `EntiteController.GetEntite` |
| ☐ | `PATCH` | `/entite` | `EntiteController.updateEntite` | `EntiteController.UpdateEntite` |
| ☐ | `GET` | `/enumerations` | `EnumerationController.getEnumerations` | `EnumerationController.GetEnumerations` |
| ☐ | `POST` | `/enumerations` | `EnumerationController.createEnumeration` | `EnumerationController.CreateEnumeration` |
| ☐ | `DELETE` | `/enumerations/:id` | `EnumerationController.deleteEnumeration` | `EnumerationController.DeleteEnumeration` |
| ☐ | `PATCH` | `/enumerations/:id` | `EnumerationController.updateEnumeration` | `EnumerationController.UpdateEnumeration` |
| ☐ | `GET` | `/fournisseurs` | `FournisseurController.getFournisseurs` | `ReferentielsController.GetFournisseurs` |
| ☐ | `GET` | `/lien-documents` | `LienDocumentController.getLiensDocuments` | `LienDocumentController.GetLiensDocuments` |
| ☐ | `POST` | `/lien-documents` | `LienDocumentController.createLienDocument` | `LienDocumentController.CreateLienDocument` |
| ☐ | `DELETE` | `/lien-documents/:id` | `LienDocumentController.deleteLienDocument` | `LienDocumentController.DeleteLienDocument` |
| ☐ | `GET` | `/lien-documents/:id/fichier` | `LienDocumentController.getFichier` | `LienDocumentController.GetFichier` |
| ☐ | `GET` | `/marchandises` | `MarchandiseController.getMarchandises` | `MarchandiseController.GetMarchandises` |
| ☐ | `POST` | `/marchandises` | `MarchandiseController.createMarchandise` | `MarchandiseController.CreateMarchandise` |
| ☐ | `DELETE` | `/marchandises/:id` | `MarchandiseController.deleteMarchandise` | `MarchandiseController.DeleteMarchandise` |
| ☐ | `GET` | `/marchandises/:id` | `MarchandiseController.getMarchandise` | `MarchandiseController.GetMarchandise` |
| ☐ | `PATCH` | `/marchandises/:id` | `MarchandiseController.updateMarchandise` | `MarchandiseController.UpdateMarchandise` |
| ☐ | `GET` | `/messages` | `MessageController.getMessages` | `ReferentielsController.GetMessages` |
| ☐ | `POST` | `/mistral/ocr` | `MistralController.ocr` | `MistralController.Ocr` |
| ☐ | `GET` | `/pays` | `PaysController.getPays` | `ReferentielsController.GetPays` |
| ☐ | `POST` | `/performance/calcul` | `PerformanceController.lancerCalcul` | `PerformanceController.LancerCalcul` |
| ☐ | `POST` | `/performance/impression-contrats` | `PerformanceController.imprimerTousLesContrats` | `PerformanceController.ImprimerTousLesContrats` |
| ☐ | `GET` | `/performance/ping` | `PerformanceController.ping` | `PerformanceController.Ping` |
| ☐ | `POST` | `/performance/traitement-long` | `PerformanceController.lancerTraitementLong` | `PerformanceController.LancerTraitementLong` |
| ☐ | `GET` | `/performance/transfert` | `PerformanceController.transfert` | `PerformanceController.Transfert` |
| ☐ | `GET` | `/personnel` | `PersonnelController.getPersonnels` | `PersonnelController.GetPersonnels` |
| ☐ | `POST` | `/personnel` | `PersonnelController.createPersonnel` | `PersonnelController.CreatePersonnel` |
| ☐ | `DELETE` | `/personnel/:id` | `PersonnelController.deletePersonnel` | `PersonnelController.DeletePersonnel` |
| ☐ | `GET` | `/personnel/:id` | `PersonnelController.getPersonnel` | `PersonnelController.GetPersonnel` |
| ☐ | `PATCH` | `/personnel/:id` | `PersonnelController.updatePersonnel` | `PersonnelController.UpdatePersonnel` |
| ☐ | `GET` | `/personnel/:id/contrats-travail` | `ContratTravailController.getContrats` | `ContratTravailController.GetContrats` |
| ☐ | `POST` | `/personnel/:id/contrats-travail` | `ContratTravailController.createContrat` | `ContratTravailController.CreateContrat` |
| ☐ | `GET` | `/planning` | `PlanningController.getPlanning` | `PlanningController.GetPlanning` |
| ☐ | `POST` | `/planning/executions` | `PlanningController.createExecution` | `PlanningController.CreateExecution` |
| ☐ | `DELETE` | `/planning/executions/:id` | `PlanningController.deleteExecution` | `PlanningController.DeleteExecution` |
| ☐ | `PATCH` | `/planning/executions/:id` | `PlanningController.moveExecution` | `PlanningController.MoveExecution` |
| ☐ | `GET` | `/pointage` | `PointageController.getPointages` | `PointageController.GetPointages` |
| ☐ | `POST` | `/pointage` | `PointageController.createPointage` | `PointageController.CreatePointage` |
| ☐ | `DELETE` | `/pointage/:id` | `PointageController.deletePointage` | `PointageController.DeletePointage` |
| ☐ | `GET` | `/pointage/:id` | `PointageController.getPointage` | `PointageController.GetPointage` |
| ☐ | `PATCH` | `/pointage/:id` | `PointageController.updatePointage` | `PointageController.UpdatePointage` |
| ☐ | `GET` | `/pointage/feuille` | `PointageController.getFeuille` | `PointageController.GetFeuille` |
| ☐ | `GET` | `/pointage/statuts` | `PointageController.getStatuts` | `PointageController.GetStatuts` |
| ☐ | `GET` | `/points` | `PointController.getPoints` | `PointController.GetPoints` |
| ☐ | `POST` | `/points` | `PointController.createPoint` | `PointController.CreatePoint` |
| ☐ | `DELETE` | `/points/:id` | `PointController.deletePoint` | `PointController.DeletePoint` |
| ☐ | `GET` | `/points/:id` | `PointController.getPoint` | `PointController.GetPoint` |
| ☐ | `PATCH` | `/points/:id` | `PointController.updatePoint` | `PointController.UpdatePoint` |
| ☐ | `DELETE` | `/points/:id/contacts/:contactId` | `PointController.removePointContact` | `PointController.RemovePointContact` |
| ☐ | `PATCH` | `/points/:id/contacts/:contactId` | `PointController.updatePointContact` | `PointController.UpdatePointContact` |
| ☐ | `GET` | `/societes` | `SocieteController.getSocietes` | `SocieteController.GetSocietes` |
| ☐ | `POST` | `/societes` | `SocieteController.createSociete` | `SocieteController.CreateSociete` |
| ☐ | `DELETE` | `/societes/:id` | `SocieteController.deleteSociete` | `SocieteController.DeleteSociete` |
| ☐ | `GET` | `/societes/:id` | `SocieteController.getSociete` | `SocieteController.GetSociete` |
| ☐ | `PATCH` | `/societes/:id` | `SocieteController.updateSociete` | `SocieteController.UpdateSociete` |
| ☐ | `GET` | `/statistiques/chiffre-affaires` | `StatistiqueController.getChiffreAffaires` | `StatistiqueController.GetChiffreAffaires` |
| ☐ | `PATCH` | `/taches/:id/reprogrammer` | `TacheController.reprogrammerTache` | `TacheController.ReprogrammerTache` |
| ☐ | `PATCH` | `/taches/:id/traiter` | `TacheController.traiterTache` | `TacheController.TraiterTache` |
| ☐ | `GET` | `/taches/a-faire` | `TacheController.getTachesAFaire` | `TacheController.GetTachesAFaire` |
| ☐ | `GET` | `/types-facture` | `TypeFactureController.getTypesFacture` | `ReferentielsController.GetTypesFacture` |
| ☐ | `GET` | `/vehicules` | `VehiculeController.getVehicules` | `VehiculeController.GetVehicules` |
| ☐ | `POST` | `/vehicules` | `VehiculeController.createVehicule` | `VehiculeController.CreateVehicule` |
| ☐ | `DELETE` | `/vehicules/:id` | `VehiculeController.deleteVehicule` | `VehiculeController.DeleteVehicule` |
| ☐ | `GET` | `/vehicules/:id` | `VehiculeController.getVehicule` | `VehiculeController.GetVehicule` |
| ☐ | `PATCH` | `/vehicules/:id` | `VehiculeController.updateVehicule` | `VehiculeController.UpdateVehicule` |
| ☐ | `GET` | `/villes` | `VilleController.getVilles` | `ReferentielsController.GetVilles` |
