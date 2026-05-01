#!/usr/bin/env python3
"""
QuadroApp — SQLite → PostgreSQL data migration script
======================================================
Run this ONCE on Thursday after PostgreSQL is installed and the new app
version has been launched at least once (so EnsureCreatedAsync has run
and all tables exist in PostgreSQL).

Usage
-----
  pip install psycopg2-binary
  python migrate_to_postgres.py

Or with explicit paths:
  python migrate_to_postgres.py --sqlite "C:/path/to/quadro.db" --pg "Host=localhost;Port=5432;Database=quadrodb;Username=quadro;Password=CHANGE_ME"

What it does
------------
  1. Opens the existing SQLite database (read-only)
  2. Disables foreign-key checks in PostgreSQL (so we can insert in any order)
  3. Copies every table row-by-row
  4. Re-enables foreign-key checks
  5. Resets all PostgreSQL identity sequences to max(id)+1

Requirements
------------
  Python 3.9+
  psycopg2-binary  (pip install psycopg2-binary)
"""

import argparse
import sqlite3
import sys
from datetime import datetime, date

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    print("ERROR: psycopg2 niet gevonden. Installeer het met:")
    print("  pip install psycopg2-binary")
    sys.exit(1)


# ── Default paths — edit these if needed ────────────────────────────────────

DEFAULT_SQLITE = "quadro.db"

DEFAULT_PG = (
    "host=localhost port=5432 dbname=quadrodb user=quadro password=CHANGE_ME"
)

# ── Table insertion order (FK-safe: parents before children) ─────────────────

TABLES = [
    "Leveranciers",
    "AfwerkingsGroepen",
    "Klanten",
    "ImportSessions",
    "Instellingen",           # PK is text (Sleutel), no Id
    "TypeLijsten",
    "AfwerkingsOpties",
    "Offertes",
    "WerkBonnen",
    "WerkTaken",
    "OfferteRegels",
    "ImportRowLogs",
    "LeverancierBestellingen",
    "LeverancierBestelLijnen",
    "VoorraadMutaties",
    "VoorraadAlerts",
    "Facturen",
    "FactuurLijnen",
    "GeblokkeerDagen",
    "WerkBonArchieven",
    "OfferteArchieven",
]

# Tables whose PK is NOT an integer identity column (no sequence to reset)
NO_SEQUENCE_TABLES = {"Instellingen"}


def parse_args():
    p = argparse.ArgumentParser(description="Migreer QuadroApp SQLite → PostgreSQL")
    p.add_argument("--sqlite", default=DEFAULT_SQLITE, help="Pad naar quadro.db")
    p.add_argument("--pg", default=DEFAULT_PG, help="PostgreSQL connection string (libpq format)")
    return p.parse_args()


def sqlite_to_pg_value(value):
    """
    Convert a SQLite value to something psycopg2 can insert into PostgreSQL.

    SQLite stores:
      - booleans as INTEGER (0 / 1)   → we leave as int; PostgreSQL casts automatically
      - datetimes as TEXT ISO strings  → we parse to Python datetime
      - everything else natively
    """
    if isinstance(value, str):
        # Try to parse ISO datetime strings that EF Core stores in SQLite
        for fmt in ("%Y-%m-%dT%H:%M:%S.%f", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d %H:%M:%S.%f", "%Y-%m-%d %H:%M:%S"):
            try:
                return datetime.strptime(value, fmt)
            except ValueError:
                continue
        # Try date-only strings
        try:
            return date.fromisoformat(value)
        except ValueError:
            pass
    return value


def migrate_table(sqlite_cur, pg_cur, table: str) -> int:
    """Copy all rows from SQLite table into PostgreSQL table. Returns row count."""

    # Read all rows from SQLite
    sqlite_cur.execute(f'SELECT * FROM "{table}"')
    rows = sqlite_cur.fetchall()
    if not rows:
        return 0

    col_names = [desc[0] for desc in sqlite_cur.description]
    placeholders = ", ".join(["%s"] * len(col_names))
    col_list = ", ".join(f'"{c}"' for c in col_names)
    sql = f'INSERT INTO "{table}" ({col_list}) VALUES ({placeholders}) ON CONFLICT DO NOTHING'

    converted = [
        tuple(sqlite_to_pg_value(v) for v in row)
        for row in rows
    ]

    psycopg2.extras.execute_batch(pg_cur, sql, converted, page_size=200)
    return len(converted)


def reset_sequences(pg_cur, table: str):
    """Reset the PostgreSQL identity sequence for a table so new inserts get correct IDs."""
    pg_cur.execute(f"""
        SELECT setval(
            pg_get_serial_sequence('"{table}"', 'Id'),
            COALESCE((SELECT MAX("Id") FROM "{table}"), 0) + 1,
            false
        )
    """)


def main():
    args = parse_args()

    print(f"\n{'='*60}")
    print(f"  QuadroApp — SQLite → PostgreSQL migratie")
    print(f"{'='*60}")
    print(f"  SQLite:     {args.sqlite}")
    print(f"  PostgreSQL: {args.pg.replace(args.pg.split('password=')[-1], '***') if 'password=' in args.pg else args.pg}")
    print()

    # ── Connect ───────────────────────────────────────────────────────────────
    try:
        sq_conn = sqlite3.connect(f"file:{args.sqlite}?mode=ro", uri=True)
        sq_conn.row_factory = sqlite3.Row
        sq_cur = sq_conn.cursor()
        print("✅ SQLite verbonden")
    except Exception as e:
        print(f"❌ SQLite verbinding mislukt: {e}")
        sys.exit(1)

    # Convert libpq-style "host=... dbname=..." or Npgsql "Host=...;Database=..." to psycopg2 DSN
    pg_dsn = args.pg
    if "Host=" in pg_dsn or "Database=" in pg_dsn:
        # Npgsql format → libpq format
        pg_dsn = (pg_dsn
                  .replace("Host=", "host=")
                  .replace("Port=", "port=")
                  .replace("Database=", "dbname=")
                  .replace("Username=", "user=")
                  .replace("Password=", "password=")
                  .replace(";", " "))

    try:
        pg_conn = psycopg2.connect(pg_dsn)
        pg_cur = pg_conn.cursor()
        print("✅ PostgreSQL verbonden")
    except Exception as e:
        print(f"❌ PostgreSQL verbinding mislukt: {e}")
        sq_conn.close()
        sys.exit(1)

    # ── Check which tables exist in SQLite ────────────────────────────────────
    sq_cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
    sqlite_tables = {row[0] for row in sq_cur.fetchall()}

    # ── Migrate ───────────────────────────────────────────────────────────────
    print()
    total_rows = 0
    failed_tables = []

    try:
        # Disable FK checks so we can insert freely
        pg_cur.execute("SET session_replication_role = replica;")

        for table in TABLES:
            if table not in sqlite_tables:
                print(f"  ⚠️  {table:<35} niet gevonden in SQLite, overgeslagen")
                continue

            try:
                count = migrate_table(sq_cur, pg_cur, table)
                total_rows += count
                print(f"  ✅ {table:<35} {count:>6} rijen")
            except Exception as e:
                print(f"  ❌ {table:<35} FOUT: {e}")
                failed_tables.append(table)
                pg_conn.rollback()
                pg_cur.execute("SET session_replication_role = replica;")  # re-disable after rollback

        # Re-enable FK checks
        pg_cur.execute("SET session_replication_role = DEFAULT;")

        if failed_tables:
            print(f"\n⚠️  {len(failed_tables)} tabel(len) mislukt: {failed_tables}")
            print("   Controleer de data en probeer opnieuw.")
            pg_conn.rollback()
            sys.exit(1)

        # ── Reset sequences ───────────────────────────────────────────────────
        print("\n  Sequenties resetten...")
        for table in TABLES:
            if table in NO_SEQUENCE_TABLES or table not in sqlite_tables:
                continue
            try:
                reset_sequences(pg_cur, table)
                print(f"  🔢 {table:<35} sequence gereset")
            except Exception as e:
                # Not all tables have an Id sequence — silently skip
                pg_conn.rollback()
                pg_cur = pg_conn.cursor()
                pg_cur.execute("SET session_replication_role = DEFAULT;")

        pg_conn.commit()

    except Exception as e:
        pg_conn.rollback()
        print(f"\n❌ Onverwachte fout: {e}")
        sq_conn.close()
        pg_conn.close()
        sys.exit(1)

    # ── Done ──────────────────────────────────────────────────────────────────
    sq_conn.close()
    pg_conn.close()

    print(f"\n{'='*60}")
    print(f"  ✅ Migratie klaar — {total_rows} rijen gekopieerd")
    print(f"{'='*60}\n")


if __name__ == "__main__":
    main()
