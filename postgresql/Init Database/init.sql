CREATE TABLE units (
  unit_erp_id VARCHAR(20) NOT NULL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  imei VARCHAR(16) NOT NULL,
  pu VARCHAR(2) NOT NULL,
  changed TIMESTAMP NOT NULL
);

ALTER TABLE IF EXISTS public.units
    OWNER to test_user;

CREATE TABLE units_groups (
  group_id VARCHAR(20) NOT NULL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  pu VARCHAR(2) NOT NULL,
  changed TIMESTAMP NOT NULL
);

ALTER TABLE IF EXISTS public.units_groups
    OWNER to test_user;

CREATE TABLE orders (
  order_erp_id VARCHAR(20) NOT NULL PRIMARY KEY,
  imei VARCHAR(16) NOT NULL,
  begin_at TIMESTAMP NOT NULL,
  end_at TIMESTAMP NOT NULL,
  changed TIMESTAMP NOT NULL
);

ALTER TABLE IF EXISTS public.orders
    OWNER to test_user;
