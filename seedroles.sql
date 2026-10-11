CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE TABLE roles (
        "Id" uuid NOT NULL,
        "Name" character varying(50) NOT NULL,
        "Description" character varying(250),
        CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "FullName" character varying(150) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE TABLE user_roles (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_user_roles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES roles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE UNIQUE INDEX "IX_roles_Name" ON roles ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE INDEX "IX_user_roles_RoleId" ON user_roles ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011134141_InitialAuth') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011134141_InitialAuth', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011141312_SeedInitialRoles') THEN
    INSERT INTO roles ("Id", "Name", "Description")
    VALUES ('a7f4a8d2-6e31-4c90-8b12-123456789001', 'Admin', 'System administrator');
    INSERT INTO roles ("Id", "Name", "Description")
    VALUES ('b8e5b9c3-7f42-4da1-9c23-123456789002', 'User', 'Regular user');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011141312_SeedInitialRoles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011141312_SeedInitialRoles', '10.0.12');
    END IF;
END $EF$;
COMMIT;

