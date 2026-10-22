#!/usr/bin/env bash
set -euo pipefail

PRIMARY_HOST="${PRIMARY_HOST:-mysql-primary}"
REPLICA_HOST="${REPLICA_HOST:-mysql-replica}"
ROOT_PASS="${MYSQL_ROOT_PASSWORD:-root_pass_2026}"
REPL_USER="${REPL_USER:-repl_user}"
REPL_PASS="${REPL_PASS:-repl_pass_2026}"

echo "[replica-setup] mysql-primary ($PRIMARY_HOST) bekleniyor..."
until mysqladmin ping -h "$PRIMARY_HOST" -uroot -p"$ROOT_PASS" --silent; do
    sleep 2
done

echo "[replica-setup] mysql-replica ($REPLICA_HOST) bekleniyor..."
until mysqladmin ping -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" --silent; do
    sleep 2
done

echo "[replica-setup] Kaynak sunucuda replikasyon kullanıcısı doğrulanıyor..."
mysql -h "$PRIMARY_HOST" -uroot -p"$ROOT_PASS" <<SQL
CREATE USER IF NOT EXISTS '${REPL_USER}'@'%' IDENTIFIED WITH mysql_native_password BY '${REPL_PASS}';
ALTER USER '${REPL_USER}'@'%' IDENTIFIED WITH mysql_native_password BY '${REPL_PASS}';
GRANT REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO '${REPL_USER}'@'%';
FLUSH PRIVILEGES;
SQL

echo "[replica-setup] Replika üzerinde GTID tabanlı replikasyon yapılandırılıyor..."
mysql -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" <<SQL
SET GLOBAL super_read_only = OFF;
SET GLOBAL read_only = OFF;
STOP REPLICA;
RESET REPLICA ALL;

CHANGE REPLICATION SOURCE TO
    SOURCE_HOST='${PRIMARY_HOST}',
    SOURCE_PORT=3306,
    SOURCE_USER='${REPL_USER}',
    SOURCE_PASSWORD='${REPL_PASS}',
    SOURCE_AUTO_POSITION=1,
    GET_SOURCE_PUBLIC_KEY=1;

START REPLICA;
SQL

echo "[replica-setup] İlk veri senkronizasyonu bekleniyor..."
for i in {1..30}; do
    KULLANICI_SAYISI=$(mysql -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" -Nse "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='yemekhane' AND table_name='kullanicilar';" 2>/dev/null || echo "0")
    if [ "$KULLANICI_SAYISI" = "1" ]; then
        KAYIT_SAYISI=$(mysql -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" -Nse "SELECT COUNT(*) FROM yemekhane.kullanicilar;" 2>/dev/null || echo "0")
        if [ "$KAYIT_SAYISI" -ge 30 ]; then
            echo "[replica-setup] Senkronizasyon tamamlandı ($KAYIT_SAYISI kullanıcı replikada doğrulandı)."
            break
        fi
    fi
    sleep 1
done

echo "[replica-setup] Replika salt okunur (read_only + super_read_only) moda alınıyor..."
mysql -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" <<SQL
SET PERSIST read_only = ON;
SET PERSIST super_read_only = ON;
SQL

echo "[replica-setup] Replikasyon durumu:"
mysql -h "$REPLICA_HOST" -uroot -p"$ROOT_PASS" -e "SHOW REPLICA STATUS\G" | grep -E "Replica_IO_Running:|Replica_SQL_Running:|Executed_Gtid_Set:|Seconds_Behind_Source:" || true

echo "[replica-setup] Kurulum başarıyla tamamlandı."

