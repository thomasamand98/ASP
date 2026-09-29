// Module racine de l'application NestJS.
// Il n'a pas de logique propre : il assemble les modules métier
// qui, eux, regroupent Controller + Service + accès Model.
// Pour ajouter une nouvelle fonctionnalité, on créerait un nouveau module
// et on l'ajouterait dans le tableau `imports` ci-dessous.
import { Module } from '@nestjs/common';
import { APP_FILTER } from '@nestjs/core';
import { PrismaModule } from './prisma/prisma.module.js';
import { SocieteModule } from './societe/societe.module.js';
import { ClientModule } from './client/client.module.js';
import { FournisseurModule } from './fournisseur/fournisseur.module.js';
import { MessageModule } from './message/message.module.js';
import { ContratModule } from './contrat/contrat.module.js';
import { ContactModule } from './contact/contact.module.js';
import { PointModule } from './point/point.module.js';
import { ConditionModule } from './condition/condition.module.js';
import { MarchandiseModule } from './marchandise/marchandise.module.js';
import { ChauffeurModule } from './chauffeur/chauffeur.module.js';
import { VehiculeModule } from './vehicule/vehicule.module.js';
import { AttelageModule } from './attelage/attelage.module.js';
import { CommandeModule } from './commande/commande.module.js';
import { PlanningModule } from './planning/planning.module.js';
import { PersonnelModule } from './personnel/personnel.module.js';
import { PointageModule } from './pointage/pointage.module.js';
import { ContratTravailModule } from './contrat-travail/contrat-travail.module.js';
import { EntiteModule } from './entite/entite.module.js';
import { VilleModule } from './ville/ville.module.js';
import { PaysModule } from './pays/pays.module.js';
import { CategorieEnumerationModule } from './categorie-enumeration/categorie-enumeration.module.js';
import { EnumerationModule } from './enumeration/enumeration.module.js';
import { ConditionCmrModule } from './condition-cmr/condition-cmr.module.js';
import { TypeFactureModule } from './type-facture/type-facture.module.js';
import { DechetModule } from './dechet/dechet.module.js';
import { DocumentTemplateModule } from './document-template/document-template.module.js';
import { DocumentMergeModule } from './document-merge/document-merge.module.js';
import { AuthModule } from './auth/auth.module.js';
import { MistralModule } from './mistral/mistral.module.js';
import { TacheModule } from './tache/tache.module.js';
import { StatistiqueModule } from './statistique/statistique.module.js';
import { AllExceptionsFilter } from './common/all-exceptions.filter.js';

@Module({
  imports: [
    AuthModule,
    PrismaModule,
    SocieteModule,
    ClientModule,
    FournisseurModule,
    MessageModule,
    ContratModule,
    ContactModule,
    PointModule,
    ConditionModule,
    MarchandiseModule,
    ChauffeurModule,
    VehiculeModule,
    AttelageModule,
    CommandeModule,
    PlanningModule,
    PersonnelModule,
    PointageModule,
    ContratTravailModule,
    EntiteModule,
    VilleModule,
    PaysModule,
    CategorieEnumerationModule,
    EnumerationModule,
    ConditionCmrModule,
    TypeFactureModule,
    DechetModule,
    DocumentTemplateModule,
    DocumentMergeModule,
    MistralModule,
    TacheModule,
    StatistiqueModule,
  ],
  providers: [{ provide: APP_FILTER, useClass: AllExceptionsFilter }],
})
export class AppModule {}
