# Итоговый проект "Кафка для разработчиков" Гонтаря Романа

Для запуска проекта необходимо установить кластер Кафки из директории kafka-cluster.
\```
cd kafka-cluster
docker-compose up -d
\```
Также необходимо установить кластер PostgreSql из директории postgresql (необходимые таблицы БД будут созданы автоматически).
\```
cd postgresql
docker-compose up -d
\```
В кластере Кафки при помощи AKHQ необходимо создать несколько топиков:
- messages
- orders
- raw_messages
- units
- units_groups
все топики создаются с фактором репликации 3, для топика raw_messages retention.ms необходимо установить 1800000 (30 минут), для всех остальных оставить по умолчанию 1 сутки.

В kSQL необходимо создать следующие стримы:
\```
create stream incoming_units (unit_erp_id varchar, name varchar, imei varchar, pu varchar, changed timestamp) with (kafka_topic='units', value_format='avro');
\```
\```
create stream incoming_units_groups (group_id varchar, name varchar, pu varchar, changed timestamp) with (kafka_topic='units_groups', value_format='avro');
\```
\```
create stream incoming_raw_messages (imei varchar, lat double, lon double, created timestamp) with (kafka_topic='raw_messages', value_format='avro');
\```
\```
create or replace stream messages with (kafka_topic='messages', value_format='avro') as
select
incoming_raw_messages.imei,
as_value(incoming_raw_messages.imei) as imei,
incoming_raw_messages.lat,
incoming_raw_messages.lon,
incoming_raw_messages.created
from incoming_raw_messages
inner join incoming_units within 5 minutes grace period 1 minutes on incoming_units.imei = incoming_raw_messages.imei
partition by incoming_raw_messages.imei;
\```

В Kafka Connector необходимо создать три Synk-коннектора со следующими настройками:
\```
{
  "name": "2-public-units",
  "connector.class": "io.confluent.connect.jdbc.JdbcSinkConnector",
  "dialect.name": "PostgreSqlDatabaseDialect",
  "table.name.format": "units",
  "connection.password": "test_pwd",
  "topics": "units",
  "connection.attempts": "3",
  "connection.backoff.ms": "3000",
  "auto.evolve": "True",
  "connection.user": "test_user",
  "db.timezone": "UTC",
  "auto.create": "False",
  "connection.url": "jdbc:postgresql://192.168.81.253:5432/pvom_db?currentSchema=public",
  "insert.mode": "upsert",
  "pk.mode": "record_value",
  "key.converter": "org.apache.kafka.connect.storage.StringConverter",
  "pk.fields": "unit_erp_id"
}
\```
\```
{
  "name": "2-public-units_groups",
  "connector.class": "io.confluent.connect.jdbc.JdbcSinkConnector",
  "dialect.name": "PostgreSqlDatabaseDialect",
  "table.name.format": "units_groups",
  "connection.password": "test_pwd",
  "topics": "units_groups",
  "connection.attempts": "3",
  "connection.backoff.ms": "3000",
  "auto.evolve": "True",
  "connection.user": "test_user",
  "db.timezone": "UTC",
  "auto.create": "False",
  "connection.url": "jdbc:postgresql://192.168.81.253:5432/pvom_db?currentSchema=public",
  "insert.mode": "upsert",
  "pk.mode": "record_value",
  "key.converter": "org.apache.kafka.connect.storage.StringConverter",
  "pk.fields": "group_id"
}
\```
\```
{
  "name": "2-public-orders",
  "connector.class": "io.confluent.connect.jdbc.JdbcSinkConnector",
  "dialect.name": "PostgreSqlDatabaseDialect",
  "table.name.format": "orders",
  "connection.password": "test_pwd",
  "topics": "orders",
  "connection.attempts": "3",
  "connection.backoff.ms": "3000",
  "auto.evolve": "True",
  "connection.user": "test_user",
  "db.timezone": "UTC",
  "auto.create": "False",
  "connection.url": "jdbc:postgresql://192.168.81.253:5432/pvom_db?currentSchema=public",
  "insert.mode": "upsert",
  "pk.mode": "record_value",
  "key.converter": "org.apache.kafka.connect.storage.StringConverter",
  "pk.fields": "order_erp_id"
}
\```

Далее можно последовательно скомпилировать и запустить проекты:
- ErpProducer.Net,
- RepeaterProducer.Net,
- GisConsumer.Net.

И после этого наблюдать за работой созданного проекта.