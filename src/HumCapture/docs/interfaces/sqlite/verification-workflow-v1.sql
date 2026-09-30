PRAGMA user_version = 1;
CREATE TABLE workflow_metadata (
 singleton INTEGER PRIMARY KEY CHECK(singleton=1),
 repository_id TEXT NOT NULL,
 schema_version TEXT NOT NULL CHECK(schema_version='1.0.0')
) STRICT;
CREATE TABLE capture_assignments (
 assignment_id TEXT PRIMARY KEY,
 capture_attempt_id TEXT NOT NULL UNIQUE,
 payload BLOB NOT NULL,
 sha256 TEXT NOT NULL CHECK(length(sha256)=64),
 windows_account TEXT NOT NULL,
 created_utc TEXT NOT NULL
) STRICT;
CREATE TABLE verification_attempts (
 attempt_id TEXT PRIMARY KEY,
 assignment_id TEXT NOT NULL REFERENCES capture_assignments(assignment_id),
 request BLOB NOT NULL,
 sha256 TEXT NOT NULL CHECK(length(sha256)=64),
 windows_account TEXT NOT NULL,
 created_utc TEXT NOT NULL
) STRICT;
CREATE TABLE verification_events (
 attempt_id TEXT NOT NULL REFERENCES verification_attempts(attempt_id),
 sequence INTEGER NOT NULL CHECK(sequence>0),
 payload BLOB NOT NULL,
 sha256 TEXT NOT NULL CHECK(length(sha256)=64),
 recorded_utc TEXT NOT NULL,
 PRIMARY KEY(attempt_id, sequence)
) STRICT;
CREATE TRIGGER assignments_no_update BEFORE UPDATE ON capture_assignments BEGIN SELECT RAISE(ABORT,'Immutable assignment'); END;
CREATE TRIGGER assignments_no_delete BEFORE DELETE ON capture_assignments BEGIN SELECT RAISE(ABORT,'Immutable assignment'); END;
CREATE TRIGGER attempts_no_update BEFORE UPDATE ON verification_attempts BEGIN SELECT RAISE(ABORT,'Immutable attempt'); END;
CREATE TRIGGER attempts_no_delete BEFORE DELETE ON verification_attempts BEGIN SELECT RAISE(ABORT,'Immutable attempt'); END;
CREATE TRIGGER events_no_update BEFORE UPDATE ON verification_events BEGIN SELECT RAISE(ABORT,'Append-only event'); END;
CREATE TRIGGER events_no_delete BEFORE DELETE ON verification_events BEGIN SELECT RAISE(ABORT,'Append-only event'); END;
