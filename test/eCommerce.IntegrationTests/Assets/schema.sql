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

