CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "CatProducts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(64) NOT NULL,
    "Name" character varying(512) NOT NULL,
    "Slug" character varying(512),
    "Description" text,
    "BrandId" uuid,
    "IsPublished" boolean NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatProducts" PRIMARY KEY ("Id")
);

CREATE TABLE "ChnChannelListings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ChannelShopId" uuid NOT NULL,
    "ChannelCode" character varying(32) NOT NULL,
    "ProductVariantId" uuid NOT NULL,
    "InternalSku" character varying(64) NOT NULL,
    "ExternalItemId" character varying(64),
    "ExternalSku" character varying(64),
    "ChannelPrice" numeric(18,4),
    "AllottedQuantity" integer,
    "Status" integer NOT NULL,
    "LastSyncError" character varying(2048),
    "LastSyncedAtUtc" timestamp with time zone,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_ChnChannelListings" PRIMARY KEY ("Id")
);

CREATE TABLE "ChnChannels" (
    "Id" uuid NOT NULL,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Type" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "HoldsInventory" boolean NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_ChnChannels" PRIMARY KEY ("Id")
);

CREATE TABLE "ChnChannelShops" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ChannelCode" character varying(32) NOT NULL,
    "ExternalShopId" character varying(64) NOT NULL,
    "ShopName" character varying(256) NOT NULL,
    "Region" character varying(16),
    "AccessTokenEncrypted" text,
    "RefreshTokenEncrypted" text,
    "AccessTokenExpiresAtUtc" timestamp with time zone,
    "RefreshTokenExpiresAtUtc" timestamp with time zone,
    "AuthorizationExpiresAtUtc" timestamp with time zone,
    "LastSyncedAtUtc" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "PushStatus" character varying(32),
    "LastError" character varying(2048),
    "SyncIntervalSeconds" integer,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_ChnChannelShops" PRIMARY KEY ("Id")
);

CREATE TABLE "InvItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "WarehouseId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "OnHand" integer NOT NULL,
    "Reserved" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvItems" PRIMARY KEY ("Id")
);

CREATE TABLE "InvStockLedger" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "WarehouseId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "MovementType" integer NOT NULL,
    "QuantityDelta" integer NOT NULL,
    "BalanceAfter" integer NOT NULL,
    "ReferenceType" character varying(64),
    "ReferenceId" character varying(128),
    "ChannelCode" character varying(32),
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "Note" character varying(2048),
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvStockLedger" PRIMARY KEY ("Id")
);

CREATE TABLE "OrdOrders" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Number" character varying(64) NOT NULL,
    "ChannelCode" character varying(32) NOT NULL,
    "ChannelShopId" uuid,
    "ExternalOrderId" character varying(64),
    "CustomerId" uuid,
    "Status" integer NOT NULL,
    "PaymentStatus" integer NOT NULL,
    "Currency" character varying(8) NOT NULL,
    "Subtotal" numeric(18,4) NOT NULL,
    "DiscountTotal" numeric(18,4) NOT NULL,
    "TaxTotal" numeric(18,4) NOT NULL,
    "ShippingTotal" numeric(18,4) NOT NULL,
    "GrandTotal" numeric(18,4) NOT NULL,
    "ChannelFeeAmount" numeric(18,4),
    "Note" character varying(2048),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_OrdOrders" PRIMARY KEY ("Id")
);

CREATE TABLE "PayTransactions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ProviderCode" character varying(32) NOT NULL,
    "OrderId" uuid,
    "MerchantReference" character varying(64) NOT NULL,
    "ProviderTransactionId" character varying(128),
    "Amount" numeric(18,4) NOT NULL,
    "Currency" character varying(8) NOT NULL,
    "Status" integer NOT NULL,
    "PaidAtUtc" timestamp with time zone,
    "RefundedAmount" numeric(18,4) NOT NULL,
    "LastWebhookPayload" text,
    "FailureReason" character varying(2000),
    "WebhookReceivedCount" integer NOT NULL,
    "IdempotencyKey" character varying(256),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PayTransactions" PRIMARY KEY ("Id")
);

CREATE TABLE "CatProductVariants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ProductId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "Barcode" character varying(64),
    "Price" numeric(18,4) NOT NULL,
    "CompareAtPrice" numeric(18,4),
    "WeightInGrams" integer,
    "IsActive" boolean NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatProductVariants" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CatProductVariants_CatProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "CatProducts" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrdOrderLines" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "OrderId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "ProductVariantId" uuid,
    "ExternalLineId" character varying(64),
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "DiscountAmount" numeric(18,4) NOT NULL,
    "TaxAmount" numeric(18,4) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_OrdOrderLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrdOrderLines_OrdOrders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "OrdOrders" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Code" ON "CatProducts" ("TenantId", "Code");

CREATE INDEX "IX_CatProductVariants_ProductId" ON "CatProductVariants" ("ProductId");

CREATE UNIQUE INDEX "IX_CatProductVariants_TenantId_Sku" ON "CatProductVariants" ("TenantId", "Sku");

CREATE INDEX "IX_ChnChannelListings_ChannelShopId_ExternalItemId" ON "ChnChannelListings" ("ChannelShopId", "ExternalItemId");

CREATE UNIQUE INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId" ON "ChnChannelListings" ("TenantId", "ChannelShopId", "ProductVariantId");

CREATE UNIQUE INDEX "IX_ChnChannels_Code" ON "ChnChannels" ("Code");

CREATE INDEX "IX_ChnChannelShops_IsActive_ChannelCode" ON "ChnChannelShops" ("IsActive", "ChannelCode");

CREATE UNIQUE INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId" ON "ChnChannelShops" ("TenantId", "ChannelCode", "ExternalShopId");

CREATE UNIQUE INDEX "IX_InvItems_TenantId_WarehouseId_Sku" ON "InvItems" ("TenantId", "WarehouseId", "Sku");

CREATE INDEX "IX_InvStockLedger_TenantId_ChannelCode_OccurredAtUtc" ON "InvStockLedger" ("TenantId", "ChannelCode", "OccurredAtUtc");

CREATE INDEX "IX_InvStockLedger_TenantId_WarehouseId_Sku_OccurredAtUtc" ON "InvStockLedger" ("TenantId", "WarehouseId", "Sku", "OccurredAtUtc");

CREATE UNIQUE INDEX "IX_OrdOrderLines_OrderId_ExternalLineId" ON "OrdOrderLines" ("OrderId", "ExternalLineId") WHERE "ExternalLineId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId" ON "OrdOrders" ("TenantId", "ChannelCode", "ExternalOrderId") WHERE "ExternalOrderId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_Number" ON "OrdOrders" ("TenantId", "Number");

CREATE INDEX "IX_PayTransactions_OrderId" ON "PayTransactions" ("OrderId");

CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey" ON "PayTransactions" ("TenantId", "ProviderCode", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference" ON "PayTransactions" ("TenantId", "ProviderCode", "MerchantReference");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009070214_Initial', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "AbpClaimTypes" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Required" boolean NOT NULL,
    "IsStatic" boolean NOT NULL,
    "Regex" character varying(512),
    "RegexDescription" character varying(128),
    "Description" character varying(256),
    "ValueType" integer NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_AbpClaimTypes" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpLinkUsers" (
    "Id" uuid NOT NULL,
    "SourceUserId" uuid NOT NULL,
    "SourceTenantId" uuid,
    "TargetUserId" uuid NOT NULL,
    "TargetTenantId" uuid,
    CONSTRAINT "PK_AbpLinkUsers" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpOrganizationUnits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ParentId" uuid,
    "Code" character varying(95) NOT NULL,
    "DisplayName" character varying(128) NOT NULL,
    "EntityVersion" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_AbpOrganizationUnits" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpOrganizationUnits_AbpOrganizationUnits_ParentId" FOREIGN KEY ("ParentId") REFERENCES "AbpOrganizationUnits" ("Id")
);

CREATE TABLE "AbpRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Name" character varying(256) NOT NULL,
    "NormalizedName" character varying(256) NOT NULL,
    "IsDefault" boolean NOT NULL,
    "IsStatic" boolean NOT NULL,
    "IsPublic" boolean NOT NULL,
    "EntityVersion" integer NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_AbpRoles" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpSecurityLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ApplicationName" character varying(96),
    "Identity" character varying(96),
    "Action" character varying(96),
    "UserId" uuid,
    "UserName" character varying(256),
    "TenantName" character varying(64),
    "ClientId" character varying(64),
    "CorrelationId" character varying(64),
    "ClientIpAddress" character varying(64),
    "BrowserInfo" character varying(512),
    "CreationTime" timestamp with time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_AbpSecurityLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpSessions" (
    "Id" uuid NOT NULL,
    "SessionId" character varying(128) NOT NULL,
    "Device" character varying(64) NOT NULL,
    "DeviceInfo" character varying(256),
    "TenantId" uuid,
    "UserId" uuid NOT NULL,
    "ClientId" character varying(64),
    "IpAddresses" character varying(2048),
    "SignedIn" timestamp with time zone NOT NULL,
    "LastAccessed" timestamp with time zone,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpSessions" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpUserDelegations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "SourceUserId" uuid NOT NULL,
    "TargetUserId" uuid NOT NULL,
    "StartTime" timestamp with time zone NOT NULL,
    "EndTime" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AbpUserDelegations" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpUsers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "UserName" character varying(256) NOT NULL,
    "NormalizedUserName" character varying(256) NOT NULL,
    "Name" character varying(64),
    "Surname" character varying(64),
    "Email" character varying(256) NOT NULL,
    "NormalizedEmail" character varying(256) NOT NULL,
    "EmailConfirmed" boolean NOT NULL DEFAULT FALSE,
    "PasswordHash" character varying(256),
    "SecurityStamp" character varying(256) NOT NULL,
    "IsExternal" boolean NOT NULL DEFAULT FALSE,
    "PhoneNumber" character varying(16),
    "PhoneNumberConfirmed" boolean NOT NULL DEFAULT FALSE,
    "IsActive" boolean NOT NULL,
    "TwoFactorEnabled" boolean NOT NULL DEFAULT FALSE,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean NOT NULL DEFAULT FALSE,
    "AccessFailedCount" integer NOT NULL DEFAULT 0,
    "ShouldChangePasswordOnNextLogin" boolean NOT NULL,
    "EntityVersion" integer NOT NULL,
    "LastPasswordChangeTime" timestamp with time zone,
    "LastSignInTime" timestamp with time zone,
    "Leaved" boolean NOT NULL DEFAULT FALSE,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_AbpUsers" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpOrganizationUnitRoles" (
    "RoleId" uuid NOT NULL,
    "OrganizationUnitId" uuid NOT NULL,
    "TenantId" uuid,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    CONSTRAINT "PK_AbpOrganizationUnitRoles" PRIMARY KEY ("OrganizationUnitId", "RoleId"),
    CONSTRAINT "FK_AbpOrganizationUnitRoles_AbpOrganizationUnits_OrganizationU~" FOREIGN KEY ("OrganizationUnitId") REFERENCES "AbpOrganizationUnits" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AbpOrganizationUnitRoles_AbpRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AbpRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpRoleClaims" (
    "Id" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "TenantId" uuid,
    "ClaimType" character varying(256) NOT NULL,
    "ClaimValue" character varying(1024),
    CONSTRAINT "PK_AbpRoleClaims" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpRoleClaims_AbpRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AbpRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserClaims" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TenantId" uuid,
    "ClaimType" character varying(256) NOT NULL,
    "ClaimValue" character varying(1024),
    CONSTRAINT "PK_AbpUserClaims" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpUserClaims_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserLogins" (
    "UserId" uuid NOT NULL,
    "LoginProvider" character varying(64) NOT NULL,
    "TenantId" uuid,
    "ProviderKey" character varying(196) NOT NULL,
    "ProviderDisplayName" character varying(128),
    CONSTRAINT "PK_AbpUserLogins" PRIMARY KEY ("UserId", "LoginProvider"),
    CONSTRAINT "FK_AbpUserLogins_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserOrganizationUnits" (
    "UserId" uuid NOT NULL,
    "OrganizationUnitId" uuid NOT NULL,
    "TenantId" uuid,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    CONSTRAINT "PK_AbpUserOrganizationUnits" PRIMARY KEY ("OrganizationUnitId", "UserId"),
    CONSTRAINT "FK_AbpUserOrganizationUnits_AbpOrganizationUnits_OrganizationU~" FOREIGN KEY ("OrganizationUnitId") REFERENCES "AbpOrganizationUnits" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AbpUserOrganizationUnits_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserPasskeys" (
    "CredentialId" bytea NOT NULL,
    "TenantId" uuid,
    "UserId" uuid NOT NULL,
    "Data" jsonb,
    CONSTRAINT "PK_AbpUserPasskeys" PRIMARY KEY ("CredentialId"),
    CONSTRAINT "FK_AbpUserPasskeys_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserPasswordHistories" (
    "UserId" uuid NOT NULL,
    "Password" character varying(256) NOT NULL,
    "TenantId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AbpUserPasswordHistories" PRIMARY KEY ("UserId", "Password"),
    CONSTRAINT "FK_AbpUserPasswordHistories_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "TenantId" uuid,
    CONSTRAINT "PK_AbpUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AbpUserRoles_AbpRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AbpRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AbpUserRoles_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpUserTokens" (
    "UserId" uuid NOT NULL,
    "LoginProvider" character varying(64) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "TenantId" uuid,
    "Value" text,
    CONSTRAINT "PK_AbpUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AbpUserTokens_AbpUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AbpUsers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_AbpLinkUsers_SourceUserId_SourceTenantId_TargetUserId_Targe~" ON "AbpLinkUsers" ("SourceUserId", "SourceTenantId", "TargetUserId", "TargetTenantId");

CREATE INDEX "IX_AbpOrganizationUnitRoles_RoleId_OrganizationUnitId" ON "AbpOrganizationUnitRoles" ("RoleId", "OrganizationUnitId");

CREATE INDEX "IX_AbpOrganizationUnits_Code" ON "AbpOrganizationUnits" ("Code");

CREATE INDEX "IX_AbpOrganizationUnits_ParentId" ON "AbpOrganizationUnits" ("ParentId");

CREATE INDEX "IX_AbpRoleClaims_RoleId" ON "AbpRoleClaims" ("RoleId");

CREATE INDEX "IX_AbpRoles_NormalizedName" ON "AbpRoles" ("NormalizedName");

CREATE INDEX "IX_AbpSecurityLogs_TenantId_Action" ON "AbpSecurityLogs" ("TenantId", "Action");

CREATE INDEX "IX_AbpSecurityLogs_TenantId_ApplicationName" ON "AbpSecurityLogs" ("TenantId", "ApplicationName");

CREATE INDEX "IX_AbpSecurityLogs_TenantId_Identity" ON "AbpSecurityLogs" ("TenantId", "Identity");

CREATE INDEX "IX_AbpSecurityLogs_TenantId_UserId" ON "AbpSecurityLogs" ("TenantId", "UserId");

CREATE INDEX "IX_AbpSessions_Device" ON "AbpSessions" ("Device");

CREATE INDEX "IX_AbpSessions_SessionId" ON "AbpSessions" ("SessionId");

CREATE INDEX "IX_AbpSessions_TenantId_UserId" ON "AbpSessions" ("TenantId", "UserId");

CREATE INDEX "IX_AbpUserClaims_UserId" ON "AbpUserClaims" ("UserId");

CREATE INDEX "IX_AbpUserLogins_LoginProvider_ProviderKey" ON "AbpUserLogins" ("LoginProvider", "ProviderKey");

CREATE INDEX "IX_AbpUserOrganizationUnits_UserId_OrganizationUnitId" ON "AbpUserOrganizationUnits" ("UserId", "OrganizationUnitId");

CREATE INDEX "IX_AbpUserPasskeys_UserId" ON "AbpUserPasskeys" ("UserId");

CREATE INDEX "IX_AbpUserRoles_RoleId_UserId" ON "AbpUserRoles" ("RoleId", "UserId");

CREATE INDEX "IX_AbpUsers_Email" ON "AbpUsers" ("Email");

CREATE INDEX "IX_AbpUsers_NormalizedEmail" ON "AbpUsers" ("NormalizedEmail");

CREATE INDEX "IX_AbpUsers_NormalizedUserName" ON "AbpUsers" ("NormalizedUserName");

CREATE INDEX "IX_AbpUsers_UserName" ON "AbpUsers" ("UserName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009102044_Added_Identity', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "AbpAuditLogExcelFiles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "FileName" character varying(256),
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    CONSTRAINT "PK_AbpAuditLogExcelFiles" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpAuditLogs" (
    "Id" uuid NOT NULL,
    "ApplicationName" character varying(96),
    "UserId" uuid,
    "UserName" character varying(256),
    "TenantId" uuid,
    "TenantName" character varying(64),
    "ImpersonatorUserId" uuid,
    "ImpersonatorUserName" character varying(256),
    "ImpersonatorTenantId" uuid,
    "ImpersonatorTenantName" character varying(64),
    "ExecutionTime" timestamp with time zone NOT NULL,
    "ExecutionDuration" integer NOT NULL,
    "ClientIpAddress" character varying(64),
    "ClientName" character varying(128),
    "ClientId" character varying(64),
    "CorrelationId" character varying(64),
    "BrowserInfo" character varying(512),
    "HttpMethod" character varying(16),
    "Url" character varying(256),
    "Exceptions" text,
    "Comments" character varying(256),
    "HttpStatusCode" integer,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_AbpAuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpBackgroundJobs" (
    "Id" uuid NOT NULL,
    "ApplicationName" character varying(96),
    "JobName" character varying(128) NOT NULL,
    "JobArgs" character varying(1048576) NOT NULL,
    "TryCount" smallint NOT NULL DEFAULT 0,
    "CreationTime" timestamp with time zone NOT NULL,
    "NextTryTime" timestamp with time zone NOT NULL,
    "LastTryTime" timestamp with time zone,
    "IsAbandoned" boolean NOT NULL DEFAULT FALSE,
    "CompletionTime" timestamp with time zone,
    "Priority" smallint NOT NULL DEFAULT 15,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_AbpBackgroundJobs" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpFeatureGroups" (
    "Id" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "DisplayName" character varying(256) NOT NULL,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpFeatureGroups" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpFeatures" (
    "Id" uuid NOT NULL,
    "GroupName" character varying(128) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "ParentName" character varying(128),
    "DisplayName" character varying(256) NOT NULL,
    "Description" character varying(256),
    "DefaultValue" character varying(256),
    "IsVisibleToClients" boolean NOT NULL,
    "IsAvailableToHost" boolean NOT NULL,
    "AllowedProviders" character varying(256),
    "ValueType" character varying(2048),
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpFeatures" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpFeatureValues" (
    "Id" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Value" character varying(128) NOT NULL,
    "ProviderName" character varying(64),
    "ProviderKey" character varying(64),
    CONSTRAINT "PK_AbpFeatureValues" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpPermissionGrants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Name" character varying(128) NOT NULL,
    "ProviderName" character varying(64) NOT NULL,
    "ProviderKey" character varying(64) NOT NULL,
    CONSTRAINT "PK_AbpPermissionGrants" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpPermissionGroups" (
    "Id" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "DisplayName" character varying(256) NOT NULL,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpPermissionGroups" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpPermissions" (
    "Id" uuid NOT NULL,
    "GroupName" character varying(128),
    "Name" character varying(128) NOT NULL,
    "ResourceName" character varying(256),
    "ManagementPermissionName" character varying(128),
    "ParentName" character varying(128),
    "DisplayName" character varying(256) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "MultiTenancySide" smallint NOT NULL,
    "Providers" character varying(128),
    "StateCheckers" character varying(256),
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpPermissions" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpResourcePermissionGrants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Name" character varying(128) NOT NULL,
    "ProviderName" character varying(64) NOT NULL,
    "ProviderKey" character varying(64) NOT NULL,
    "ResourceName" character varying(256) NOT NULL,
    "ResourceKey" character varying(256) NOT NULL,
    CONSTRAINT "PK_AbpResourcePermissionGrants" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpSettingDefinitions" (
    "Id" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "DisplayName" character varying(256) NOT NULL,
    "Description" character varying(512),
    "DefaultValue" character varying(2048),
    "IsVisibleToClients" boolean NOT NULL,
    "Providers" character varying(1024),
    "IsInherited" boolean NOT NULL,
    "IsEncrypted" boolean NOT NULL,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpSettingDefinitions" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpSettings" (
    "Id" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Value" character varying(2048) NOT NULL,
    "ProviderName" character varying(64),
    "ProviderKey" character varying(64),
    CONSTRAINT "PK_AbpSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "OpenIddictApplications" (
    "Id" uuid NOT NULL,
    "ApplicationType" character varying(50),
    "ClientId" character varying(100),
    "ClientSecret" text,
    "ClientType" character varying(50),
    "ConsentType" character varying(50),
    "DisplayName" text,
    "DisplayNames" text,
    "JsonWebKeySet" text,
    "Permissions" text,
    "PostLogoutRedirectUris" text,
    "Properties" text,
    "RedirectUris" text,
    "Requirements" text,
    "Settings" text,
    "FrontChannelLogoutUri" text,
    "ClientUri" text,
    "LogoUri" text,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
);

CREATE TABLE "OpenIddictScopes" (
    "Id" uuid NOT NULL,
    "Description" text,
    "Descriptions" text,
    "DisplayName" text,
    "DisplayNames" text,
    "Name" character varying(200),
    "Properties" text,
    "Resources" text,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
);

CREATE TABLE "AbpAuditLogActions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "AuditLogId" uuid NOT NULL,
    "ServiceName" character varying(256),
    "MethodName" character varying(128),
    "Parameters" character varying(2000),
    "ExecutionTime" timestamp with time zone NOT NULL,
    "ExecutionDuration" integer NOT NULL,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpAuditLogActions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpAuditLogActions_AbpAuditLogs_AuditLogId" FOREIGN KEY ("AuditLogId") REFERENCES "AbpAuditLogs" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AbpEntityChanges" (
    "Id" uuid NOT NULL,
    "AuditLogId" uuid NOT NULL,
    "TenantId" uuid,
    "ChangeTime" timestamp with time zone NOT NULL,
    "ChangeType" smallint NOT NULL,
    "EntityTenantId" uuid,
    "EntityId" character varying(128),
    "EntityTypeFullName" character varying(512) NOT NULL,
    "ExtraProperties" text,
    CONSTRAINT "PK_AbpEntityChanges" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpEntityChanges_AbpAuditLogs_AuditLogId" FOREIGN KEY ("AuditLogId") REFERENCES "AbpAuditLogs" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OpenIddictAuthorizations" (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid,
    "CreationDate" timestamp with time zone,
    "Properties" text,
    "Scopes" text,
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(50),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id")
);

CREATE TABLE "AbpEntityPropertyChanges" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "EntityChangeId" uuid NOT NULL,
    "NewValue" character varying(512),
    "OriginalValue" character varying(512),
    "PropertyName" character varying(128) NOT NULL,
    "PropertyTypeFullName" character varying(512) NOT NULL,
    CONSTRAINT "PK_AbpEntityPropertyChanges" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AbpEntityPropertyChanges_AbpEntityChanges_EntityChangeId" FOREIGN KEY ("EntityChangeId") REFERENCES "AbpEntityChanges" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OpenIddictTokens" (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid,
    "AuthorizationId" uuid,
    "CreationDate" timestamp with time zone,
    "ExpirationDate" timestamp with time zone,
    "Payload" text,
    "Properties" text,
    "RedemptionDate" timestamp with time zone,
    "ReferenceId" character varying(100),
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(150),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES "OpenIddictAuthorizations" ("Id")
);

CREATE INDEX "IX_AbpAuditLogActions_AuditLogId" ON "AbpAuditLogActions" ("AuditLogId");

CREATE INDEX "IX_AbpAuditLogActions_TenantId_ServiceName_MethodName_Executio~" ON "AbpAuditLogActions" ("TenantId", "ServiceName", "MethodName", "ExecutionTime");

CREATE INDEX "IX_AbpAuditLogs_TenantId_ExecutionTime" ON "AbpAuditLogs" ("TenantId", "ExecutionTime");

CREATE INDEX "IX_AbpAuditLogs_TenantId_UserId_ExecutionTime" ON "AbpAuditLogs" ("TenantId", "UserId", "ExecutionTime");

CREATE INDEX "IX_AbpBackgroundJobs_ApplicationName_CompletionTime_IsAbandone~" ON "AbpBackgroundJobs" ("ApplicationName", "CompletionTime", "IsAbandoned", "NextTryTime");

CREATE INDEX "IX_AbpEntityChanges_AuditLogId" ON "AbpEntityChanges" ("AuditLogId");

CREATE INDEX "IX_AbpEntityChanges_TenantId_EntityTypeFullName_EntityId" ON "AbpEntityChanges" ("TenantId", "EntityTypeFullName", "EntityId");

CREATE INDEX "IX_AbpEntityPropertyChanges_EntityChangeId" ON "AbpEntityPropertyChanges" ("EntityChangeId");

CREATE UNIQUE INDEX "IX_AbpFeatureGroups_Name" ON "AbpFeatureGroups" ("Name");

CREATE INDEX "IX_AbpFeatures_GroupName" ON "AbpFeatures" ("GroupName");

CREATE UNIQUE INDEX "IX_AbpFeatures_Name" ON "AbpFeatures" ("Name");

CREATE UNIQUE INDEX "IX_AbpFeatureValues_Name_ProviderName_ProviderKey" ON "AbpFeatureValues" ("Name", "ProviderName", "ProviderKey");

CREATE UNIQUE INDEX "IX_AbpPermissionGrants_TenantId_Name_ProviderName_ProviderKey" ON "AbpPermissionGrants" ("TenantId", "Name", "ProviderName", "ProviderKey");

CREATE UNIQUE INDEX "IX_AbpPermissionGroups_Name" ON "AbpPermissionGroups" ("Name");

CREATE INDEX "IX_AbpPermissions_GroupName" ON "AbpPermissions" ("GroupName");

CREATE UNIQUE INDEX "IX_AbpPermissions_ResourceName_Name" ON "AbpPermissions" ("ResourceName", "Name");

CREATE UNIQUE INDEX "IX_AbpResourcePermissionGrants_TenantId_Name_ResourceName_Reso~" ON "AbpResourcePermissionGrants" ("TenantId", "Name", "ResourceName", "ResourceKey", "ProviderName", "ProviderKey");

CREATE UNIQUE INDEX "IX_AbpSettingDefinitions_Name" ON "AbpSettingDefinitions" ("Name");

CREATE UNIQUE INDEX "IX_AbpSettings_Name_ProviderName_ProviderKey" ON "AbpSettings" ("Name", "ProviderName", "ProviderKey");

CREATE INDEX "IX_OpenIddictApplications_ClientId" ON "OpenIddictApplications" ("ClientId");

CREATE INDEX "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type" ON "OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");

CREATE INDEX "IX_OpenIddictScopes_Name" ON "OpenIddictScopes" ("Name");

CREATE INDEX "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON "OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");

CREATE INDEX "IX_OpenIddictTokens_AuthorizationId" ON "OpenIddictTokens" ("AuthorizationId");

CREATE INDEX "IX_OpenIddictTokens_ReferenceId" ON "OpenIddictTokens" ("ReferenceId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009102627_Added_Phase0_AuthModules', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "TaxClassifications" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "TargetType" integer NOT NULL,
    "TargetId" uuid NOT NULL,
    "BaseRateCode" character varying(32) NOT NULL,
    "ReductionApplicable" boolean NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    "LegalBasisNote" character varying(512),
    "Status" integer NOT NULL,
    "ApprovedBy" uuid,
    "ApprovedAtUtc" timestamp with time zone,
    "Version" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_TaxClassifications" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_TaxClassifications_EffectiveRange" CHECK ("EffectiveTo" IS NULL OR "EffectiveTo" >= "EffectiveFrom")
);

CREATE TABLE "TaxRateSchedules" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Rate" numeric(9,4) NOT NULL,
    "Kind" integer NOT NULL,
    "ReducedFromCode" character varying(32),
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    "LegalBasis" character varying(512),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_TaxRateSchedules" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_TaxRateSchedules_EffectiveRange" CHECK ("EffectiveTo" IS NULL OR "EffectiveTo" >= "EffectiveFrom"),
    CONSTRAINT "CK_TaxRateSchedules_Rate" CHECK ("Rate" >= 0 AND "Rate" <= 100)
);

CREATE UNIQUE INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~" ON "TaxClassifications" ("TenantId", "TargetType", "TargetId", "EffectiveFrom");

CREATE INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_Status" ON "TaxClassifications" ("TenantId", "TargetType", "TargetId", "Status");

CREATE UNIQUE INDEX "IX_TaxRateSchedules_TenantId_Code" ON "TaxRateSchedules" ("TenantId", "Code");

CREATE INDEX "IX_TaxRateSchedules_TenantId_EffectiveFrom_EffectiveTo" ON "TaxRateSchedules" ("TenantId", "EffectiveFrom", "EffectiveTo");

CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE "TaxClassifications"
    ADD CONSTRAINT "EX_TaxClassifications_ChongChongKhung"
    EXCLUDE USING gist (
        (COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
        "TargetType" WITH =,
        "TargetId" WITH =,
        "IsDeleted" WITH =,
        daterange("EffectiveFrom", COALESCE("EffectiveTo", 'infinity'::date), '[]') WITH &&
    );

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013012648_Add_Tax_Module', '10.0.11');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_CatProducts_TenantId_Code";
CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Code"
    ON "CatProducts" ("TenantId", "Code") NULLS NOT DISTINCT;

DROP INDEX "IX_CatProductVariants_TenantId_Sku";
CREATE UNIQUE INDEX "IX_CatProductVariants_TenantId_Sku"
    ON "CatProductVariants" ("TenantId", "Sku") NULLS NOT DISTINCT;

DROP INDEX "IX_InvItems_TenantId_WarehouseId_Sku";
CREATE UNIQUE INDEX "IX_InvItems_TenantId_WarehouseId_Sku"
    ON "InvItems" ("TenantId", "WarehouseId", "Sku") NULLS NOT DISTINCT;

DROP INDEX "IX_OrdOrders_TenantId_Number";
CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_Number"
    ON "OrdOrders" ("TenantId", "Number") NULLS NOT DISTINCT;

DROP INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId";
CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId"
    ON "OrdOrders" ("TenantId", "ChannelCode", "ExternalOrderId") NULLS NOT DISTINCT
    WHERE ("ExternalOrderId" IS NOT NULL);

DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference";
CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference"
    ON "PayTransactions" ("TenantId", "ProviderCode", "MerchantReference") NULLS NOT DISTINCT;

DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey";
CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey"
    ON "PayTransactions" ("TenantId", "ProviderCode", "IdempotencyKey") NULLS NOT DISTINCT
    WHERE ("IdempotencyKey" IS NOT NULL);

DROP INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId";
CREATE UNIQUE INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId"
    ON "ChnChannelShops" ("TenantId", "ChannelCode", "ExternalShopId") NULLS NOT DISTINCT;

DROP INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId";
CREATE UNIQUE INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId"
    ON "ChnChannelListings" ("TenantId", "ChannelShopId", "ProductVariantId") NULLS NOT DISTINCT;

DROP INDEX "IX_TaxRateSchedules_TenantId_Code";
CREATE UNIQUE INDEX "IX_TaxRateSchedules_TenantId_Code"
    ON "TaxRateSchedules" ("TenantId", "Code") NULLS NOT DISTINCT;

-- ⚠️ Tên index này bị EF CẮT ở 63 ký tự và kết thúc bằng '~' — đúng tên thật trong DB.
DROP INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~";
CREATE UNIQUE INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~"
    ON "TaxClassifications" ("TenantId", "TargetType", "TargetId", "EffectiveFrom") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013024226_Fix_NullableTenantId_UniqueIndexes', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "MsgIdempotencyRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Scope" character varying(64) NOT NULL,
    "KeyValue" character varying(256) NOT NULL,
    "PayloadHash" character varying(64) NOT NULL,
    "PayloadHashVersion" integer NOT NULL,
    "State" integer NOT NULL,
    "Result" text,
    "ErrorCode" character varying(64),
    "ErrorMessage" character varying(1024),
    "Attempts" integer NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_MsgIdempotencyRecords" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MsgIdempotencyRecords_Attempts" CHECK ("Attempts" >= 1)
);

CREATE INDEX "IX_MsgIdempotencyRecords_ExpiresAtUtc" ON "MsgIdempotencyRecords" ("ExpiresAtUtc");

CREATE UNIQUE INDEX "IX_MsgIdempotencyRecords_TenantId_Scope_KeyValue"
    ON "MsgIdempotencyRecords" ("TenantId", "Scope", "KeyValue")
    NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013092108_Add_Messaging_Idempotency', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "AddrAdministrativeUnits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "SchemaVersion" integer NOT NULL,
    "Level" integer NOT NULL,
    "Code" character varying(5) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "ParentCode" character varying(5),
    "IsActive" boolean NOT NULL,
    "LegalBasis" character varying(512),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_AddrAdministrativeUnits" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_AddrAdministrativeUnits_SchemaVersion" CHECK ("SchemaVersion" in (1, 2))
);

CREATE INDEX "IX_AddrAdministrativeUnits_SchemaVersion_Level_ParentCode" ON "AddrAdministrativeUnits" ("SchemaVersion", "Level", "ParentCode");

CREATE INDEX "IX_AddrAdministrativeUnits_SchemaVersion_Name" ON "AddrAdministrativeUnits" ("SchemaVersion", "Name");

CREATE UNIQUE INDEX "IX_AddrAdministrativeUnits_TenantId_SchemaVersion_Level_Code"
    ON "AddrAdministrativeUnits" ("TenantId", "SchemaVersion", "Level", "Code")
    NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013093715_Add_Address_AdministrativeUnits', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "EInvInvoices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "KyHieuMauSo" character varying(32) NOT NULL,
    "SoHoaDon" character varying(8) NOT NULL,
    "NgayLap" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "TaxAuthorityStatus" integer NOT NULL,
    "MaCQT" character varying(64),
    "LyDo" character varying(1024),
    "HoaDonBiDieuChinhId" uuid,
    "HoaDonBiThayTheId" uuid,
    "LoiCQT" character varying(2048),
    "XmlDaKy" text,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_EInvInvoices" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_EInvInvoices_PhaiKyTruocKhiPhatHanh" CHECK ("Status" = 0 OR "Status" = 5 OR "XmlDaKy" IS NOT NULL)
);

CREATE INDEX "IX_EInvInvoices_HoaDonBiDieuChinhId" ON "EInvInvoices" ("HoaDonBiDieuChinhId");

CREATE INDEX "IX_EInvInvoices_HoaDonBiThayTheId" ON "EInvInvoices" ("HoaDonBiThayTheId");

CREATE INDEX "IX_EInvInvoices_Status_TaxAuthorityStatus" ON "EInvInvoices" ("Status", "TaxAuthorityStatus");

CREATE UNIQUE INDEX "IX_EInvInvoices_TenantId_KyHieuMauSo_SoHoaDon"
    ON "EInvInvoices" ("TenantId", "KyHieuMauSo", "SoHoaDon")
    NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013094411_Add_EInvoice_Invoices', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "SigCertificates" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "SoSerial" character varying(128) NOT NULL,
    "ChuSoHuu" character varying(512) NOT NULL,
    "DonViPhatHanh" character varying(256) NOT NULL,
    "HieuLucTu" timestamp with time zone NOT NULL,
    "HieuLucDen" timestamp with time zone NOT NULL,
    "TrangThai" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_SigCertificates" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_SigCertificates_HieuLuc" CHECK ("HieuLucDen" > "HieuLucTu")
);

CREATE TABLE "SigSigningRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "MaTaiLieu" character varying(128) NOT NULL,
    "HashTaiLieu" character varying(128) NOT NULL,
    "ThuatToanBam" character varying(32) NOT NULL,
    "TrangThai" integer NOT NULL,
    "ChungThuSoId" uuid,
    "ChuKyDaKy" text,
    "ThoiDiemKy" timestamp with time zone,
    "NguoiKy" character varying(256),
    "LyDoHuy" character varying(1024),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_SigSigningRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_SigSigningRequests_DaKyPhaiCoDuBangChung" CHECK ("TrangThai" <> 1 OR ("ChuKyDaKy" IS NOT NULL AND "ThoiDiemKy" IS NOT NULL AND "NguoiKy" IS NOT NULL AND "ChungThuSoId" IS NOT NULL))
);

CREATE UNIQUE INDEX "IX_SigCertificates_TenantId_SoSerial" ON "SigCertificates" ("TenantId", "SoSerial") NULLS NOT DISTINCT;

CREATE UNIQUE INDEX "IX_SigSigningRequests_TenantId_MaTaiLieu" ON "SigSigningRequests" ("TenantId", "MaTaiLieu") NULLS NOT DISTINCT;

CREATE INDEX "IX_SigSigningRequests_TrangThai" ON "SigSigningRequests" ("TrangThai");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014012331_Add_Signing_Tables', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "EInvInvoices" ADD "LanGuiCuoiUtc" timestamp with time zone;

ALTER TABLE "EInvInvoices" ADD "SoLanGuiCQT" integer NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014031859_Add_EInvoice_CqtRetryCounters', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "AddrCarrierMappings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "CarrierCode" character varying(32) NOT NULL,
    "CarrierLevel" character varying(32) NOT NULL,
    "CarrierAddressCode" character varying(64) NOT NULL,
    "CarrierAddressName" character varying(256) NOT NULL,
    "OurAdministrativeUnitId" uuid NOT NULL,
    "MatchMethod" integer NOT NULL,
    "Status" integer NOT NULL,
    "Source" character varying(512),
    "SyncedAtUtc" timestamp with time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_AddrCarrierMappings" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_AddrCarrierMappings_Status" CHECK ("Status" in (0, 1, 2))
);

CREATE INDEX "IX_AddrCarrierMappings_Status_CarrierCode" ON "AddrCarrierMappings" ("Status", "CarrierCode");

CREATE UNIQUE INDEX "IX_AddrCarrierMappings_TenantId_CarrierCode_CarrierAddressCode" ON "AddrCarrierMappings" ("TenantId", "CarrierCode", "CarrierAddressCode") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014042922_Add_Addr002_CarrierMappings', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE UNIQUE INDEX "IX_EInvInvoices_TenantId_HoaDonBiDieuChinhId" ON "EInvInvoices" ("TenantId", "HoaDonBiDieuChinhId") NULLS NOT DISTINCT WHERE "HoaDonBiDieuChinhId" IS NOT NULL AND NOT "IsDeleted";

CREATE UNIQUE INDEX "IX_EInvInvoices_TenantId_HoaDonBiThayTheId" ON "EInvInvoices" ("TenantId", "HoaDonBiThayTheId") NULLS NOT DISTINCT WHERE "HoaDonBiThayTheId" IS NOT NULL AND NOT "IsDeleted";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014095047_Add_Einv003_UniqueReplacementLink', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "SalMarketplaceOrderIntakes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ChannelCode" character varying(32) NOT NULL,
    "ExternalShopId" character varying(64) NOT NULL,
    "ExternalOrderId" character varying(64) NOT NULL,
    "OrderId" uuid,
    "MarketplacePlacedAtUtc" timestamp with time zone,
    "PayloadHash" character varying(64) NOT NULL,
    "ReceivedAtUtc" timestamp with time zone NOT NULL,
    "CorrelationId" character varying(64),
    "SkipReason" character varying(500),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_SalMarketplaceOrderIntakes" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_SalMktIntakes_TenantId_ChannelCode_ExternalOrderId" ON "SalMarketplaceOrderIntakes" ("TenantId", "ChannelCode", "ExternalOrderId") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015082700_Add_Sal005_MarketplaceOrderIntake', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "CatProductVariants" DROP CONSTRAINT "FK_CatProductVariants_CatProducts_ProductId";

ALTER TABLE "CatProducts" ADD "Status" integer NOT NULL DEFAULT 0;

UPDATE "CatProducts" SET "Status" = 1 WHERE "IsPublished" = true;

ALTER TABLE "CatProducts" DROP COLUMN "IsPublished";

ALTER TABLE "CatProductVariants" ADD CONSTRAINT "FK_CatProductVariants_CatProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "CatProducts" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015085029_Cat001_ProductStatus_And_RestrictVariantFk', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "CatCategories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ParentId" uuid,
    "Code" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Slug" character varying(256),
    "Path" character varying(1024) NOT NULL,
    "Level" integer NOT NULL,
    "SortOrder" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "MaxVariantCombinations" integer,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatCategories" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CatCategories_Level_Khoang" CHECK ("Level" >= 0 AND "Level" <= 5),
    CONSTRAINT "CK_CatCategories_Path_DinhDang" CHECK ("Path" LIKE '/%/'),
    CONSTRAINT "FK_CatCategories_CatCategories_ParentId" FOREIGN KEY ("ParentId") REFERENCES "CatCategories" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_CatCategories_ParentId" ON "CatCategories" ("ParentId");

CREATE UNIQUE INDEX "IX_CatCategories_TenantId_Code" ON "CatCategories" ("TenantId", "Code");

CREATE INDEX "IX_CatCategories_TenantId_ParentId" ON "CatCategories" ("TenantId", "ParentId");

CREATE INDEX "IX_CatCategories_TenantId_Path" ON "CatCategories" ("TenantId", "Path");

DROP INDEX "IX_CatCategories_TenantId_Code";

CREATE UNIQUE INDEX "IX_CatCategories_TenantId_Code" ON "CatCategories" ("TenantId", "Code") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015092958_Cat002_CategoryTree', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "CatProductCategories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ProductId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatProductCategories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CatProductCategories_CatCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "CatCategories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CatProductCategories_CatProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "CatProducts" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_CatProdCats_TenantId_CategoryId" ON "CatProductCategories" ("TenantId", "CategoryId");

CREATE UNIQUE INDEX "IX_CatProdCats_TenantId_ProductId_CategoryId" ON "CatProductCategories" ("TenantId", "ProductId", "CategoryId");

CREATE UNIQUE INDEX "IX_CatProdCats_TenantId_ProductId_ChiMotChinh" ON "CatProductCategories" ("TenantId", "ProductId") WHERE "IsPrimary";

CREATE INDEX "IX_CatProductCategories_CategoryId" ON "CatProductCategories" ("CategoryId");

CREATE INDEX "IX_CatProductCategories_ProductId" ON "CatProductCategories" ("ProductId");

DROP INDEX "IX_CatProdCats_TenantId_ProductId_CategoryId";

CREATE UNIQUE INDEX "IX_CatProdCats_TenantId_ProductId_CategoryId" ON "CatProductCategories" ("TenantId", "ProductId", "CategoryId") NULLS NOT DISTINCT;

DROP INDEX "IX_CatProdCats_TenantId_ProductId_ChiMotChinh";

CREATE UNIQUE INDEX "IX_CatProdCats_TenantId_ProductId_ChiMotChinh" ON "CatProductCategories" ("TenantId", "ProductId") NULLS NOT DISTINCT WHERE "IsPrimary";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015095145_Cat002b_ProductCategory', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "CatProductVariants" ADD "AttributeHash" character varying(128);

CREATE TABLE "CatAttributes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Usage" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatAttributes" PRIMARY KEY ("Id")
);

CREATE TABLE "CatAttributeValues" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "AttributeId" uuid NOT NULL,
    "Value" character varying(256) NOT NULL,
    "SortOrder" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatAttributeValues" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CatAttributeValues_CatAttributes_AttributeId" FOREIGN KEY ("AttributeId") REFERENCES "CatAttributes" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CatProductAttributeSelections" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ProductId" uuid NOT NULL,
    "AttributeId" uuid NOT NULL,
    "AttributeValueId" uuid NOT NULL,
    "SortOrder" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatProductAttributeSelections" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CatProductAttributeSelections_CatAttributeValues_AttributeV~" FOREIGN KEY ("AttributeValueId") REFERENCES "CatAttributeValues" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CatProductAttributeSelections_CatAttributes_AttributeId" FOREIGN KEY ("AttributeId") REFERENCES "CatAttributes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CatProductAttributeSelections_CatProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "CatProducts" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CatProductVariantAttributes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ProductVariantId" uuid NOT NULL,
    "AttributeId" uuid NOT NULL,
    "AttributeValueId" uuid NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CatProductVariantAttributes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CatProductVariantAttributes_CatAttributeValues_AttributeVal~" FOREIGN KEY ("AttributeValueId") REFERENCES "CatAttributeValues" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CatProductVariantAttributes_CatAttributes_AttributeId" FOREIGN KEY ("AttributeId") REFERENCES "CatAttributes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CatProductVariantAttributes_CatProductVariants_ProductVaria~" FOREIGN KEY ("ProductVariantId") REFERENCES "CatProductVariants" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_CatProdVariants_TenantId_ProductId_AttributeHash" ON "CatProductVariants" ("TenantId", "ProductId", "AttributeHash") WHERE "AttributeHash" IS NOT NULL;

CREATE UNIQUE INDEX "IX_CatAttributes_TenantId_Code" ON "CatAttributes" ("TenantId", "Code");

CREATE INDEX "IX_CatAttributeValues_AttributeId" ON "CatAttributeValues" ("AttributeId");

CREATE UNIQUE INDEX "IX_CatAttrValues_TenantId_AttributeId_Value" ON "CatAttributeValues" ("TenantId", "AttributeId", "Value");

CREATE UNIQUE INDEX "IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId" ON "CatProductAttributeSelections" ("TenantId", "ProductId", "AttributeId", "AttributeValueId");

CREATE INDEX "IX_CatProductAttributeSelections_AttributeId" ON "CatProductAttributeSelections" ("AttributeId");

CREATE INDEX "IX_CatProductAttributeSelections_AttributeValueId" ON "CatProductAttributeSelections" ("AttributeValueId");

CREATE INDEX "IX_CatProductAttributeSelections_ProductId" ON "CatProductAttributeSelections" ("ProductId");

CREATE INDEX "IX_CatProductVariantAttributes_AttributeId" ON "CatProductVariantAttributes" ("AttributeId");

CREATE INDEX "IX_CatProductVariantAttributes_AttributeValueId" ON "CatProductVariantAttributes" ("AttributeValueId");

CREATE INDEX "IX_CatProductVariantAttributes_ProductVariantId" ON "CatProductVariantAttributes" ("ProductVariantId");

CREATE UNIQUE INDEX "IX_CatProdVarAttr_TenantId_VariantId_AttributeId" ON "CatProductVariantAttributes" ("TenantId", "ProductVariantId", "AttributeId");

DROP INDEX "IX_CatAttributes_TenantId_Code";

CREATE UNIQUE INDEX "IX_CatAttributes_TenantId_Code" ON "CatAttributes" ("TenantId", "Code") NULLS NOT DISTINCT;

DROP INDEX "IX_CatAttrValues_TenantId_AttributeId_Value";

CREATE UNIQUE INDEX "IX_CatAttrValues_TenantId_AttributeId_Value" ON "CatAttributeValues" ("TenantId", "AttributeId", "Value") NULLS NOT DISTINCT;

DROP INDEX "IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId";

CREATE UNIQUE INDEX "IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId" ON "CatProductAttributeSelections" ("TenantId", "ProductId", "AttributeId", "AttributeValueId") NULLS NOT DISTINCT;

DROP INDEX "IX_CatProdVarAttr_TenantId_VariantId_AttributeId";

CREATE UNIQUE INDEX "IX_CatProdVarAttr_TenantId_VariantId_AttributeId" ON "CatProductVariantAttributes" ("TenantId", "ProductVariantId", "AttributeId") NULLS NOT DISTINCT;

DROP INDEX "IX_CatProdVariants_TenantId_ProductId_AttributeHash";

CREATE UNIQUE INDEX "IX_CatProdVariants_TenantId_ProductId_AttributeHash" ON "CatProductVariants" ("TenantId", "ProductId", "AttributeHash") NULLS NOT DISTINCT WHERE "AttributeHash" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015100156_Cat002c_AttributesAndVariantGeneration', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "PrcChannelPrices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ChannelCode" character varying(64) NOT NULL,
    "ChannelShopId" uuid,
    "Sku" character varying(64) NOT NULL,
    "MinQuantity" integer NOT NULL,
    "AmountValue" numeric(18,4) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveTo" timestamp with time zone,
    "Priority" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcChannelPrices" PRIMARY KEY ("Id")
);

CREATE TABLE "PrcPriceLists" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Scope" integer NOT NULL,
    "ChannelCode" character varying(64),
    "ChannelShopId" uuid,
    "Currency" character varying(8) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveTo" timestamp with time zone,
    "Priority" integer NOT NULL,
    "Status" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcPriceLists" PRIMARY KEY ("Id")
);

CREATE TABLE "PrcPriceListItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "PriceListId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "AmountValue" numeric(18,4) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcPriceListItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PrcPriceListItems_PrcPriceLists_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "PrcPriceLists" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty" ON "PrcChannelPrices" ("TenantId", "ChannelCode", "ChannelShopId", "Sku", "MinQuantity");

CREATE INDEX "IX_PrcChannelPrices_TenantId_Sku_ChannelCode" ON "PrcChannelPrices" ("TenantId", "Sku", "ChannelCode");

CREATE INDEX "IX_PrcPriceListItems_PriceListId" ON "PrcPriceListItems" ("PriceListId");

CREATE UNIQUE INDEX "IX_PrcPriceListItems_TenantId_PriceListId_Sku" ON "PrcPriceListItems" ("TenantId", "PriceListId", "Sku");

CREATE UNIQUE INDEX "IX_PrcPriceLists_TenantId_Code_EffectiveFrom" ON "PrcPriceLists" ("TenantId", "Code", "EffectiveFrom");

CREATE INDEX "IX_PrcPriceLists_TenantId_Status_Scope_ChannelCode" ON "PrcPriceLists" ("TenantId", "Status", "Scope", "ChannelCode");

DROP INDEX "IX_PrcPriceLists_TenantId_Code_EffectiveFrom";

CREATE UNIQUE INDEX "IX_PrcPriceLists_TenantId_Code_EffectiveFrom" ON "PrcPriceLists" ("TenantId", "Code", "EffectiveFrom") NULLS NOT DISTINCT;

DROP INDEX "IX_PrcPriceListItems_TenantId_PriceListId_Sku";

CREATE UNIQUE INDEX "IX_PrcPriceListItems_TenantId_PriceListId_Sku" ON "PrcPriceListItems" ("TenantId", "PriceListId", "Sku") NULLS NOT DISTINCT;

DROP INDEX "IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty";

CREATE UNIQUE INDEX "IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty" ON "PrcChannelPrices" ("TenantId", "ChannelCode", "ChannelShopId", "Sku", "MinQuantity") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015101649_Pricing_PriceList_And_ChannelPrice', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "PrcTierPriceSets" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Scope" integer NOT NULL,
    "ChannelCode" character varying(64),
    "Currency" character varying(8) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveTo" timestamp with time zone,
    "Priority" integer NOT NULL,
    "Status" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcTierPriceSets" PRIMARY KEY ("Id")
);

CREATE TABLE "PrcTierPrices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "TierPriceSetId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "MinQuantity" integer NOT NULL,
    "AmountValue" numeric(18,4) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcTierPrices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PrcTierPrices_PrcTierPriceSets_TierPriceSetId" FOREIGN KEY ("TierPriceSetId") REFERENCES "PrcTierPriceSets" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PrcTierPrices_TenantId_SetId_Sku_MinQty" ON "PrcTierPrices" ("TenantId", "TierPriceSetId", "Sku", "MinQuantity");

CREATE INDEX "IX_PrcTierPrices_TierPriceSetId" ON "PrcTierPrices" ("TierPriceSetId");

CREATE UNIQUE INDEX "IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom" ON "PrcTierPriceSets" ("TenantId", "Code", "EffectiveFrom");

CREATE INDEX "IX_PrcTierPriceSets_TenantId_Status_Scope_ChannelCode" ON "PrcTierPriceSets" ("TenantId", "Status", "Scope", "ChannelCode");

DROP INDEX "IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom";

CREATE UNIQUE INDEX "IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom" ON "PrcTierPriceSets" ("TenantId", "Code", "EffectiveFrom") NULLS NOT DISTINCT;

DROP INDEX "IX_PrcTierPrices_TenantId_SetId_Sku_MinQty";

CREATE UNIQUE INDEX "IX_PrcTierPrices_TenantId_SetId_Sku_MinQty" ON "PrcTierPrices" ("TenantId", "TierPriceSetId", "Sku", "MinQuantity") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015104259_Prc002_TierPrices', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "PrcTierPriceSets" ADD "ChannelShopId" uuid;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015105644_Prc002_TierPriceSet_ChannelShopId', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "PrcPriceCalculationRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "IdempotencyKey" character varying(128) NOT NULL,
    "ChannelCode" character varying(64) NOT NULL,
    "ChannelShopId" uuid,
    "CalculatedAt" timestamp with time zone NOT NULL,
    "RoundingPolicyCode" character varying(64) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "Subtotal" numeric(18,4) NOT NULL,
    "TotalDiscount" numeric(18,4) NOT NULL,
    "TotalFee" numeric(18,4) NOT NULL,
    "TotalTax" numeric(18,4) NOT NULL,
    "Total" numeric(18,4) NOT NULL,
    "Payload" text NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_PrcPriceCalculationRecords" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_PrcPriceCalculationRecords_TenantId_CalculatedAt" ON "PrcPriceCalculationRecords" ("TenantId", "CalculatedAt");

CREATE UNIQUE INDEX "IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey" ON "PrcPriceCalculationRecords" ("TenantId", "IdempotencyKey");

DROP INDEX "IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey";

CREATE UNIQUE INDEX "IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey" ON "PrcPriceCalculationRecords" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015110229_Prc002_PriceCalculationRecords', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "InvStockLedger" ADD "ReferenceLineId" uuid;

CREATE UNIQUE INDEX "IX_InvStockLedger_TenantId_ReferenceLineId" ON "InvStockLedger" ("TenantId", "ReferenceLineId") WHERE "ReferenceLineId" IS NOT NULL;

DROP INDEX "IX_InvStockLedger_TenantId_ReferenceLineId";

CREATE UNIQUE INDEX "IX_InvStockLedger_TenantId_ReferenceLineId" ON "InvStockLedger" ("TenantId", "ReferenceLineId") NULLS NOT DISTINCT WHERE "ReferenceLineId" IS NOT NULL;

ALTER TABLE "InvItems" ADD CONSTRAINT "CK_InvItems_OnHand_KhongAm" CHECK ("OnHand" >= 0);

ALTER TABLE "InvItems" ADD CONSTRAINT "CK_InvItems_Reserved_KhongAm" CHECK ("Reserved" >= 0);

ALTER TABLE "InvItems" ADD CONSTRAINT "CK_InvItems_Reserved_KhongVuotOnHand" CHECK ("Reserved" <= "OnHand");

ALTER TABLE "InvStockLedger" ADD CONSTRAINT "CK_InvStockLedger_Balance_KhongAm" CHECK ("BalanceAfter" >= 0);

ALTER TABLE "InvStockLedger" ADD CONSTRAINT "CK_InvStockLedger_Delta_KhacKhong" CHECK ("QuantityDelta" <> 0);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015111042_Inv001_ReferenceLineId_Checks_Unique', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "InvStockDocuments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "Number" character varying(64) NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "ToWarehouseId" uuid,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "Note" character varying(2048),
    "IdempotencyKey" character varying(128),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvStockDocuments" PRIMARY KEY ("Id")
);

CREATE TABLE "InvWarehouses" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Address" character varying(512),
    "IsActive" boolean NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvWarehouses" PRIMARY KEY ("Id")
);

CREATE TABLE "InvStockDocumentLines" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "StockDocumentId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "Quantity" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvStockDocumentLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_InvStockDocumentLines_InvStockDocuments_StockDocumentId" FOREIGN KEY ("StockDocumentId") REFERENCES "InvStockDocuments" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_InvStockDocumentLines_StockDocumentId" ON "InvStockDocumentLines" ("StockDocumentId");

CREATE UNIQUE INDEX "IX_InvStockDocumentLines_TenantId_Doc_Sku" ON "InvStockDocumentLines" ("TenantId", "StockDocumentId", "Sku");

CREATE UNIQUE INDEX "IX_InvStockDocuments_TenantId_Number" ON "InvStockDocuments" ("TenantId", "Number");

CREATE INDEX "IX_InvStockDocuments_TenantId_Status_Type_OccurredAtUtc" ON "InvStockDocuments" ("TenantId", "Status", "Type", "OccurredAtUtc");

CREATE UNIQUE INDEX "IX_InvWarehouses_TenantId_Code" ON "InvWarehouses" ("TenantId", "Code");

DROP INDEX "IX_InvWarehouses_TenantId_Code";

CREATE UNIQUE INDEX "IX_InvWarehouses_TenantId_Code" ON "InvWarehouses" ("TenantId", "Code") NULLS NOT DISTINCT;

DROP INDEX "IX_InvStockDocuments_TenantId_Number";

CREATE UNIQUE INDEX "IX_InvStockDocuments_TenantId_Number" ON "InvStockDocuments" ("TenantId", "Number") NULLS NOT DISTINCT;

DROP INDEX "IX_InvStockDocumentLines_TenantId_Doc_Sku";

CREATE UNIQUE INDEX "IX_InvStockDocumentLines_TenantId_Doc_Sku" ON "InvStockDocumentLines" ("TenantId", "StockDocumentId", "Sku") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015112247_Inv001_Warehouse_StockDocument', '10.0.11');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_InvStockLedger_TenantId_ReferenceLineId";

CREATE UNIQUE INDEX "IX_InvStockLedger_TenantId_ReferenceLineId" ON "InvStockLedger" ("TenantId", "ReferenceLineId", "MovementType") WHERE "ReferenceLineId" IS NOT NULL;

DROP INDEX "IX_InvStockLedger_TenantId_ReferenceLineId";

CREATE UNIQUE INDEX "IX_InvStockLedger_TenantId_ReferenceLineId" ON "InvStockLedger" ("TenantId", "ReferenceLineId", "MovementType") NULLS NOT DISTINCT WHERE "ReferenceLineId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015113253_Inv001_Fix_SoCai_Unique_ThemMovementType', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "InvStockReservations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "WarehouseId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "Quantity" integer NOT NULL,
    "Status" integer NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "ReferenceType" character varying(64),
    "ReferenceId" character varying(128),
    "ReleasedAtUtc" timestamp with time zone,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvStockReservations" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_InvStockReservations_TenantId_Ref_Sku_Kho" ON "InvStockReservations" ("TenantId", "ReferenceId", "Sku", "WarehouseId") WHERE "ReferenceId" IS NOT NULL;

CREATE INDEX "IX_InvStockReservations_TenantId_Status_ExpiresAtUtc" ON "InvStockReservations" ("TenantId", "Status", "ExpiresAtUtc");

DROP INDEX "IX_InvStockReservations_TenantId_Ref_Sku_Kho";

CREATE UNIQUE INDEX "IX_InvStockReservations_TenantId_Ref_Sku_Kho" ON "InvStockReservations" ("TenantId", "ReferenceId", "Sku", "WarehouseId") NULLS NOT DISTINCT WHERE "ReferenceId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015115023_Inv002_StockReservation', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "OrdOrders" ADD "CheckoutIdempotencyKey" character varying(128);

CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_CheckoutIdempotencyKey" ON "OrdOrders" ("TenantId", "CheckoutIdempotencyKey") WHERE "CheckoutIdempotencyKey" IS NOT NULL;

DROP INDEX "IX_OrdOrders_TenantId_CheckoutIdempotencyKey";

CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_CheckoutIdempotencyKey" ON "OrdOrders" ("TenantId", "CheckoutIdempotencyKey") NULLS NOT DISTINCT WHERE "CheckoutIdempotencyKey" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261015121002_Sal001_Order_CheckoutKey', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "InvChannelLimits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ChannelCode" character varying(32) NOT NULL,
    "WarehouseId" uuid,
    "GiaTri" numeric(18,4) NOT NULL,
    "HanhDong" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_InvChannelLimits" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_InvChannelLimits_GiaTri_KhongAm" CHECK ("GiaTri" >= 0)
);

CREATE TABLE "ReconRuns" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "BatDauUtc" timestamp with time zone NOT NULL,
    "KetThucUtc" timestamp with time zone NOT NULL,
    "SoCapDaDo" integer NOT NULL,
    "SoLech" integer NOT NULL,
    "DonViNguong" integer NOT NULL,
    "GiaTriNguong" numeric(18,4) NOT NULL,
    "HanhDong" integer NOT NULL,
    "NguongHopLe" boolean NOT NULL,
    "SoLechVuotNguong" integer NOT NULL,
    "CoVuotNguong" boolean NOT NULL,
    "KetLuan" integer NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_ReconRuns" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ReconRuns_MocThoiGian_HopLe" CHECK ("KetThucUtc" >= "BatDauUtc"),
    CONSTRAINT "CK_ReconRuns_SoCapDaDo_KhongAm" CHECK ("SoCapDaDo" >= 0),
    CONSTRAINT "CK_ReconRuns_SoLechVuot_KhongVuotTong" CHECK ("SoLechVuotNguong" >= 0 AND "SoLech" >= 0 AND "SoLechVuotNguong" <= "SoLech")
);

CREATE TABLE "ReconRunMismatches" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "ReconRunId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "Sku" character varying(64) NOT NULL,
    "OnHand" integer NOT NULL,
    "TongSoCai" integer NOT NULL,
    "ChenhLech" integer NOT NULL,
    "VuotNguong" boolean NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_ReconRunMismatches" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ReconRunMismatches_ReconRuns_ReconRunId" FOREIGN KEY ("ReconRunId") REFERENCES "ReconRuns" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId" ON "InvChannelLimits" ("TenantId", "ChannelCode", "WarehouseId");

CREATE INDEX "IX_ReconRunMismatches_ReconRunId" ON "ReconRunMismatches" ("ReconRunId");

CREATE UNIQUE INDEX "IX_ReconRunMismatches_TenantId_Run_Kho_Sku" ON "ReconRunMismatches" ("TenantId", "ReconRunId", "WarehouseId", "Sku");

CREATE INDEX "IX_ReconRuns_TenantId_BatDauUtc" ON "ReconRuns" ("TenantId", "BatDauUtc");

CREATE INDEX "IX_ReconRuns_TenantId_CoVuotNguong_BatDauUtc" ON "ReconRuns" ("TenantId", "CoVuotNguong", "BatDauUtc");

DROP INDEX "IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId";

CREATE UNIQUE INDEX "IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId" ON "InvChannelLimits" ("TenantId", "ChannelCode", "WarehouseId") NULLS NOT DISTINCT;

DROP INDEX "IX_ReconRunMismatches_TenantId_Run_Kho_Sku";

CREATE UNIQUE INDEX "IX_ReconRunMismatches_TenantId_Run_Kho_Sku" ON "ReconRunMismatches" ("TenantId", "ReconRunId", "WarehouseId", "Sku") NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261016034601_Inv003_ChannelLimits_Rec001_ReconRuns', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Slug" ON "CatProducts" ("TenantId", "Slug") WHERE "Slug" IS NOT NULL;

DROP INDEX "IX_CatProducts_TenantId_Slug";

CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Slug" ON "CatProducts" ("TenantId", "Slug") NULLS NOT DISTINCT WHERE "Slug" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261016070004_Cat003_ProductSlug_Unique', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "CusCustomers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Name" character varying(256) NOT NULL,
    "Email" character varying(256),
    "Phone" character varying(32),
    "IdentityUserId" uuid,
    "Status" integer NOT NULL,
    "CustomerGroupId" uuid,
    "LifetimeValueAmount" numeric(18,2),
    "LifetimeValueCurrency" character varying(3),
    "IdempotencyKey" character varying(128),
    "Note" character varying(1024),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CusCustomers" PRIMARY KEY ("Id")
);

CREATE TABLE "CusGroups" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Description" character varying(512),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp with time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp with time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp with time zone,
    CONSTRAINT "PK_CusGroups" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_CusCustomers_TenantId_IdempotencyKey" ON "CusCustomers" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

CREATE UNIQUE INDEX "IX_CusCustomers_TenantId_IdentityUserId" ON "CusCustomers" ("TenantId", "IdentityUserId") WHERE "IdentityUserId" IS NOT NULL;

CREATE INDEX "IX_CusCustomers_TenantId_Status" ON "CusCustomers" ("TenantId", "Status");

CREATE UNIQUE INDEX "IX_CusGroups_TenantId_Code" ON "CusGroups" ("TenantId", "Code");

DROP INDEX "IX_CusGroups_TenantId_Code";

CREATE UNIQUE INDEX "IX_CusGroups_TenantId_Code" ON "CusGroups" ("TenantId", "Code") NULLS NOT DISTINCT;

DROP INDEX "IX_CusCustomers_TenantId_IdentityUserId";

CREATE UNIQUE INDEX "IX_CusCustomers_TenantId_IdentityUserId" ON "CusCustomers" ("TenantId", "IdentityUserId") NULLS NOT DISTINCT WHERE "IdentityUserId" IS NOT NULL;

DROP INDEX "IX_CusCustomers_TenantId_IdempotencyKey";

CREATE UNIQUE INDEX "IX_CusCustomers_TenantId_IdempotencyKey" ON "CusCustomers" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT WHERE "IdempotencyKey" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261016081435_Cus001_Customers_CustomerGroups', '10.0.11');

COMMIT;

