PRAGMA foreign_keys = OFF;

------------------------------------------------------------
-- Device_type
------------------------------------------------------------

DROP TABLE IF EXISTS Device_type;

CREATE TABLE Device_type (
    ID   INTEGER PRIMARY KEY,
    type TEXT NOT NULL UNIQUE
);

INSERT INTO Device_type (type)
SELECT 'my' WHERE NOT EXISTS (SELECT 1 FROM Device_type WHERE type='my');

INSERT INTO Device_type (type)
SELECT 'other' WHERE NOT EXISTS (SELECT 1 FROM Device_type WHERE type='other');


------------------------------------------------------------
-- Device
------------------------------------------------------------

CREATE TABLE IF NOT EXISTS Device (
    ID      INTEGER PRIMARY KEY,
    ip      TEXT    UNIQUE,
    type    INTEGER,
    name    TEXT    UNIQUE,
    muted   INTEGER DEFAULT 0,
    blocked INTEGER DEFAULT 0,
    FOREIGN KEY(type) REFERENCES Device_type(ID)
);

CREATE TABLE IF NOT EXISTS Device_new (
    ID      INTEGER PRIMARY KEY,
    ip      TEXT    UNIQUE,
    type    INTEGER,
    name    TEXT    UNIQUE,
    muted   INTEGER DEFAULT 0,
    blocked INTEGER DEFAULT 0,
    FOREIGN KEY(type) REFERENCES Device_type(ID)
);

INSERT INTO Device_new (ID)
SELECT ID FROM Device;

UPDATE Device_new
SET ip = (SELECT ip FROM Device WHERE Device.ID = Device_new.ID);

UPDATE Device_new
SET type = (SELECT type FROM Device WHERE Device.ID = Device_new.ID);

UPDATE Device_new
SET name = (SELECT name FROM Device WHERE Device.ID = Device_new.ID);

UPDATE Device_new
SET muted = (SELECT muted FROM Device WHERE Device.ID = Device_new.ID);

UPDATE Device_new
SET blocked = (SELECT blocked FROM Device WHERE Device.ID = Device_new.ID);

DROP TABLE Device;
ALTER TABLE Device_new RENAME TO Device;


------------------------------------------------------------
-- Message_type
------------------------------------------------------------

DROP TABLE IF EXISTS Message_type;

CREATE TABLE Message_type (
    ID   INTEGER PRIMARY KEY,
    type TEXT NOT NULL UNIQUE
);

INSERT INTO Message_type (type)
SELECT 'message' WHERE NOT EXISTS (SELECT 1 FROM Message_type WHERE type='message');

INSERT INTO Message_type (type)
SELECT 'file' WHERE NOT EXISTS (SELECT 1 FROM Message_type WHERE type='file');


------------------------------------------------------------
-- Message
------------------------------------------------------------

CREATE TABLE IF NOT EXISTS Message (
    ID            INTEGER PRIMARY KEY,
    sender        INTEGER,
    addressee     INTEGER,
    message_type  INTEGER,
    text          TEXT   ,
    date          TEXT   ,
    viewed        INTEGER DEFAULT 0,
    FOREIGN KEY(sender)       REFERENCES Device(ID),
    FOREIGN KEY(addressee)    REFERENCES Device(ID),
    FOREIGN KEY(message_type) REFERENCES Message_type(ID)
);

CREATE TABLE IF NOT EXISTS Message_new (
    ID            INTEGER PRIMARY KEY,
    sender        INTEGER,
    addressee     INTEGER,
    message_type  INTEGER,
    text          TEXT   ,
    date          TEXT   ,
    viewed        INTEGER DEFAULT 0,
    FOREIGN KEY(sender)       REFERENCES Device(ID),
    FOREIGN KEY(addressee)    REFERENCES Device(ID),
    FOREIGN KEY(message_type) REFERENCES Message_type(ID)
);

INSERT INTO Message_new (ID)
SELECT ID FROM Message;

UPDATE Message_new
SET sender = (SELECT sender FROM Message WHERE Message.ID = Message_new.ID);

UPDATE Message_new
SET addressee = (SELECT addressee FROM Message WHERE Message.ID = Message_new.ID);

UPDATE Message_new
SET message_type = (SELECT message_type FROM Message WHERE Message.ID = Message_new.ID);

UPDATE Message_new
SET text = (SELECT text FROM Message WHERE Message.ID = Message_new.ID);

UPDATE Message_new
SET date = (SELECT date FROM Message WHERE Message.ID = Message_new.ID);

UPDATE Message_new
SET viewed = (SELECT viewed FROM Message WHERE Message.ID = Message_new.ID);

DROP TABLE Message;
ALTER TABLE Message_new RENAME TO Message;

PRAGMA foreign_keys = ON;