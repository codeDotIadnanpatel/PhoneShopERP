-- =============================================================================
-- Migration: 010-platform/0010-reference-data.sql
-- Owner: platform
-- Decisions: ADR-0014 (D-003 R7)
--
-- Vendor-owned global reference data. These tables deliberately carry no
-- tenant_id and no RLS: they are identical for every tenant and read-only to
-- the runtime role. No tax rate, jurisdiction rule, price or business policy is
-- seeded here.
-- =============================================================================

CREATE TABLE platform.currency (
    code text NOT NULL,
    numeric_code smallint NULL,
    minor_unit smallint NULL,
    display_name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_currency PRIMARY KEY (code),
    CONSTRAINT ck_currency_code CHECK (code ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_currency_numeric_code CHECK (numeric_code IS NULL OR numeric_code BETWEEN 1 AND 999),
    CONSTRAINT ck_currency_minor_unit CHECK (minor_unit IS NULL OR minor_unit BETWEEN 0 AND 4)
);

COMMENT ON TABLE platform.currency IS
    'ISO 4217 currency reference. Codes are scope metadata only; monetary precision is governed by AG-04 and no money column exists in Phase 01.';

CREATE TABLE platform.country (
    code text NOT NULL,
    alpha3_code text NOT NULL,
    display_name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_country PRIMARY KEY (code),
    CONSTRAINT ux_country_alpha3_code UNIQUE (alpha3_code),
    CONSTRAINT ck_country_code CHECK (code ~ '^[A-Z]{2}$'),
    CONSTRAINT ck_country_alpha3_code CHECK (alpha3_code ~ '^[A-Z]{3}$')
);

CREATE TABLE platform.tax_category (
    code text NOT NULL,
    display_name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_tax_category PRIMARY KEY (code),
    CONSTRAINT ck_tax_category_code CHECK (code ~ '^[a-z][a-z0-9_]{1,62}$')
);

COMMENT ON TABLE platform.tax_category IS
    'Category labels only. Rates, inclusive/exclusive rules and jurisdiction behaviour are not implemented (AG-04 / D-009 open).';

INSERT INTO platform.currency (code, numeric_code, minor_unit, display_name) VALUES
    ('AED', 784, 2, 'UAE dirham'),
    ('AUD', 36, 2, 'Australian dollar'),
    ('BDT', 50, 2, 'Bangladeshi taka'),
    ('BRL', 986, 2, 'Brazilian real'),
    ('CAD', 124, 2, 'Canadian dollar'),
    ('CHF', 756, 2, 'Swiss franc'),
    ('CNY', 156, 2, 'Chinese yuan'),
    ('EGP', 818, 2, 'Egyptian pound'),
    ('EUR', 978, 2, 'Euro'),
    ('GBP', 826, 2, 'Pound sterling'),
    ('GHS', 936, 2, 'Ghanaian cedi'),
    ('IDR', 360, 2, 'Indonesian rupiah'),
    ('INR', 356, 2, 'Indian rupee'),
    ('JPY', 392, 0, 'Japanese yen'),
    ('KES', 404, 2, 'Kenyan shilling'),
    ('LKR', 144, 2, 'Sri Lankan rupee'),
    ('MXN', 484, 2, 'Mexican peso'),
    ('MYR', 458, 2, 'Malaysian ringgit'),
    ('NGN', 566, 2, 'Nigerian naira'),
    ('NPR', 524, 2, 'Nepalese rupee'),
    ('PHP', 608, 2, 'Philippine peso'),
    ('PKR', 586, 2, 'Pakistani rupee'),
    ('RWF', 646, 0, 'Rwandan franc'),
    ('SAR', 682, 2, 'Saudi riyal'),
    ('SGD', 702, 2, 'Singapore dollar'),
    ('THB', 764, 2, 'Thai baht'),
    ('TRY', 949, 2, 'Turkish lira'),
    ('TZS', 834, 2, 'Tanzanian shilling'),
    ('UGX', 800, 0, 'Ugandan shilling'),
    ('USD', 840, 2, 'United States dollar'),
    ('VND', 704, 0, 'Vietnamese dong'),
    ('ZAR', 710, 2, 'South African rand')
ON CONFLICT (code) DO NOTHING;

INSERT INTO platform.country (code, alpha3_code, display_name) VALUES
    ('AE', 'ARE', 'United Arab Emirates'),
    ('AU', 'AUS', 'Australia'),
    ('BD', 'BGD', 'Bangladesh'),
    ('BR', 'BRA', 'Brazil'),
    ('CA', 'CAN', 'Canada'),
    ('CH', 'CHE', 'Switzerland'),
    ('CN', 'CHN', 'China'),
    ('DE', 'DEU', 'Germany'),
    ('EG', 'EGY', 'Egypt'),
    ('ES', 'ESP', 'Spain'),
    ('FR', 'FRA', 'France'),
    ('GB', 'GBR', 'United Kingdom'),
    ('GH', 'GHA', 'Ghana'),
    ('ID', 'IDN', 'Indonesia'),
    ('IN', 'IND', 'India'),
    ('IT', 'ITA', 'Italy'),
    ('JP', 'JPN', 'Japan'),
    ('KE', 'KEN', 'Kenya'),
    ('LK', 'LKA', 'Sri Lanka'),
    ('MX', 'MEX', 'Mexico'),
    ('MY', 'MYS', 'Malaysia'),
    ('NG', 'NGA', 'Nigeria'),
    ('NP', 'NPL', 'Nepal'),
    ('PH', 'PHL', 'Philippines'),
    ('PK', 'PAK', 'Pakistan'),
    ('RW', 'RWA', 'Rwanda'),
    ('SA', 'SAU', 'Saudi Arabia'),
    ('SG', 'SGP', 'Singapore'),
    ('TH', 'THA', 'Thailand'),
    ('TR', 'TUR', 'Türkiye'),
    ('TZ', 'TZA', 'Tanzania'),
    ('UG', 'UGA', 'Uganda'),
    ('US', 'USA', 'United States'),
    ('VN', 'VNM', 'Viet Nam'),
    ('ZA', 'ZAF', 'South Africa')
ON CONFLICT (code) DO NOTHING;

INSERT INTO platform.tax_category (code, display_name) VALUES
    ('exempt', 'Exempt'),
    ('reduced', 'Reduced rate'),
    ('standard', 'Standard rate'),
    ('zero', 'Zero rated')
ON CONFLICT (code) DO NOTHING;

REVOKE ALL ON TABLE platform.currency FROM PUBLIC;
REVOKE ALL ON TABLE platform.country FROM PUBLIC;
REVOKE ALL ON TABLE platform.tax_category FROM PUBLIC;

GRANT SELECT ON TABLE platform.currency TO erp_app, erp_platform_admin;
GRANT SELECT ON TABLE platform.country TO erp_app, erp_platform_admin;
GRANT SELECT ON TABLE platform.tax_category TO erp_app, erp_platform_admin;
