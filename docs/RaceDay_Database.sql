/* ============================================================
   RaceDay Event Management System
   PROG6212 - POE Part 1

   This script creates the RaceDay database, builds the tables
   from the ERD, adds sample data, and tests the main
   relationships between the tables.
   ============================================================ */


/* 1. CREATE THE DATABASE
   ------------------------------------------------------------
   The database is only created if it does not already exist.
   This prevents an error if the script is run more than once.
   ============================================================ */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO


/* 2. REMOVE OLD TABLES IF THEY EXIST
   ------------------------------------------------------------
   This section makes it possible to run the script again
   while developing and testing.
   ============================================================ */

IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL
    DROP TABLE dbo.Results;

IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL
    DROP TABLE dbo.Enrolments;

IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    DROP TABLE dbo.Categories;

IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL
    DROP TABLE dbo.Events;

IF OBJECT_ID('dbo.EnrolmentStatuses', 'U') IS NOT NULL
    DROP TABLE dbo.EnrolmentStatuses;

IF OBJECT_ID('dbo.EventTypes', 'U') IS NOT NULL
    DROP TABLE dbo.EventTypes;

IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;
GO


/* 3. USERS TABLE
   ------------------------------------------------------------
   This table stores both Participants and Organisers.
   ============================================================ */

CREATE TABLE Users
(
    UserID INT IDENTITY(1,1) PRIMARY KEY,

    FirstName NVARCHAR(50) NOT NULL,

    LastName NVARCHAR(50) NOT NULL,

    Email NVARCHAR(100) NOT NULL UNIQUE,

    PasswordHash NVARCHAR(255) NOT NULL,

    PhoneNumber NVARCHAR(20) NULL,

    Role NVARCHAR(20) NOT NULL,

    CONSTRAINT CK_Users_Role
        CHECK (Role IN ('Organiser', 'Participant'))
);
GO


/* 4. EVENT TYPES TABLE
   ------------------------------------------------------------
   This table stores the different types of events available
   on RaceDay, such as Run, Walk and Cycle.
   ============================================================ */

CREATE TABLE EventTypes
(
    EventTypeID INT IDENTITY(1,1) PRIMARY KEY,

    TypeName NVARCHAR(30) NOT NULL UNIQUE,

    Description NVARCHAR(255) NULL
);
GO


/* 5. EVENTS TABLE
   ------------------------------------------------------------
   This table stores the main information about each RaceDay
   event.
   ============================================================ */

CREATE TABLE Events
(
    EventID INT IDENTITY(1,1) PRIMARY KEY,

    EventName NVARCHAR(100) NOT NULL,

    Description NVARCHAR(500) NULL,

    EventDate DATE NOT NULL,

    Location NVARCHAR(150) NOT NULL,

    Distance DECIMAL(6,2) NOT NULL,

    EventTypeID INT NOT NULL,

    OrganiserID INT NOT NULL,

    CONSTRAINT FK_Events_EventType
        FOREIGN KEY (EventTypeID)
        REFERENCES EventTypes(EventTypeID),

    CONSTRAINT FK_Events_Organiser
        FOREIGN KEY (OrganiserID)
        REFERENCES Users(UserID),

    CONSTRAINT CK_Events_Distance
        CHECK (Distance > 0)
);
GO


/* 6. ENROLMENT STATUSES TABLE
   ------------------------------------------------------------
   This table stores the possible statuses of an enrolment.
   ============================================================ */

CREATE TABLE EnrolmentStatuses
(
    EnrolmentStatusID INT IDENTITY(1,1) PRIMARY KEY,

    StatusName NVARCHAR(30) NOT NULL UNIQUE,

    Description NVARCHAR(255) NULL
);
GO


/* 7. CATEGORIES TABLE
   ------------------------------------------------------------
   Categories belong to a specific event.
   ============================================================ */

CREATE TABLE Categories
(
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,

    EventID INT NOT NULL,

    CategoryName NVARCHAR(100) NOT NULL,

    Description NVARCHAR(255) NULL,

    CONSTRAINT FK_Categories_Event
        FOREIGN KEY (EventID)
        REFERENCES Events(EventID),

    CONSTRAINT UQ_Categories_Event_Category
        UNIQUE (EventID, CategoryName)
);
GO


/* 8. ENROLMENTS TABLE
   ------------------------------------------------------------
   This table records Participants entering events.
   Each enrolment connects a Participant, Event, Category
   and Enrolment Status.
   ============================================================ */

CREATE TABLE Enrolments
(
    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,

    ParticipantID INT NOT NULL,

    EventID INT NOT NULL,

    CategoryID INT NOT NULL,

    EnrolmentStatusID INT NOT NULL,

    EnrolmentDate DATE NOT NULL
        DEFAULT CAST(GETDATE() AS DATE),

    CONSTRAINT FK_Enrolments_Participant
        FOREIGN KEY (ParticipantID)
        REFERENCES Users(UserID),

    CONSTRAINT FK_Enrolments_Event
        FOREIGN KEY (EventID)
        REFERENCES Events(EventID),

    CONSTRAINT FK_Enrolments_Category
        FOREIGN KEY (CategoryID)
        REFERENCES Categories(CategoryID),

    CONSTRAINT FK_Enrolments_Status
        FOREIGN KEY (EnrolmentStatusID)
        REFERENCES EnrolmentStatuses(EnrolmentStatusID),

    CONSTRAINT UQ_Enrolment_Participant_Event
        UNIQUE (ParticipantID, EventID)
);
GO


/* 9. RESULTS TABLE
   ------------------------------------------------------------
   This table stores the result of a completed enrolment.
   ============================================================ */

CREATE TABLE Results
(
    ResultID INT IDENTITY(1,1) PRIMARY KEY,

    EnrolmentID INT NOT NULL UNIQUE,

    FinishTime TIME NOT NULL,

    FinishPosition INT NOT NULL,

    CONSTRAINT FK_Results_Enrolment
        FOREIGN KEY (EnrolmentID)
        REFERENCES Enrolments(EnrolmentID),

    CONSTRAINT CK_Results_FinishPosition
        CHECK (FinishPosition > 0)
);
GO


/* 10. ADD EVENT TYPES
   ------------------------------------------------------------
   These are the starting event types available in RaceDay.
   ============================================================ */

INSERT INTO EventTypes
    (TypeName, Description)
VALUES
    ('Run', 'Running events organised through RaceDay'),
    ('Walk', 'Walking events organised through RaceDay'),
    ('Cycle', 'Cycling events organised through RaceDay');
GO


/* 11. ADD ENROLMENT STATUSES
   ------------------------------------------------------------
   These statuses are used to keep track of a Participant's
   enrolment in an event.
   ============================================================ */

INSERT INTO EnrolmentStatuses
    (StatusName, Description)
VALUES
    ('Pending',
     'The enrolment has been created but is awaiting confirmation'),

    ('Confirmed',
     'The participant enrolment has been confirmed'),

    ('Cancelled',
     'The participant enrolment has been cancelled');
GO


/* 12. ADD SAMPLE USERS
   ------------------------------------------------------------
   The sample data includes both Organisers and Participants
   so that the relationships in the database can be tested.
   ============================================================ */

INSERT INTO Users
    (FirstName, LastName, Email, PasswordHash, PhoneNumber, Role)
VALUES
    ('Thabo', 'Mokoena',
     'thabo.mokoena@raceday.co.za',
     'HASH_Thabo123',
     '0712345678',
     'Organiser'),

    ('Nomsa', 'Dlamini',
     'nomsa.dlamini@raceday.co.za',
     'HASH_Nomsa123',
     '0723456789',
     'Organiser'),

    ('Ayanda', 'Mthembu',
     'ayanda.mthembu@example.com',
     'HASH_Ayanda123',
     '0734567890',
     'Participant'),

    ('Sipho', 'Khumalo',
     'sipho.khumalo@example.com',
     'HASH_Sipho123',
     '0745678901',
     'Participant'),

    ('Lerato', 'Molefe',
     'lerato.molefe@example.com',
     'HASH_Lerato123',
     '0756789012',
     'Participant');
GO


/* 13. ADD SAMPLE EVENTS
   ------------------------------------------------------------
   Three events are added so that the database demonstrates
   different event types and organisers.
   ============================================================ */

INSERT INTO Events
    (EventName, Description, EventDate, Location,
     Distance, EventTypeID, OrganiserID)
VALUES
    ('Nelson Mandela Bay Run',
     'A community running event through Nelson Mandela Bay.',
     '2026-10-10',
     'Gqeberha',
     10.00,
     1,
     1),

    ('Cape Town Coastal Cycle',
     'A cycling event along the Cape Town coastline.',
     '2026-11-14',
     'Cape Town',
     50.00,
     3,
     2),

    ('Soweto Community Walk',
     'A community walking event promoting health and participation.',
     '2026-12-05',
     'Soweto',
     8.00,
     2,
     1);
GO


/* 14. ADD EVENT CATEGORIES
   ------------------------------------------------------------
   Each event receives its own categories.
   ============================================================ */

INSERT INTO Categories
    (EventID, CategoryName, Description)
VALUES
    (1,
     '10km Open',
     'Open category for the 10km running event'),

    (1,
     '10km Under 20',
     'Category for participants under 20 years old'),

    (2,
     '50km Open',
     'Open category for the 50km cycling event'),

    (2,
     '50km Veterans',
     'Veteran category for the cycling event'),

    (3,
     '8km Open',
     'Open category for the 8km walking event'),

    (3,
     '8km Family',
     'Family category for the walking event');
GO


/* 15. ADD SAMPLE ENROLMENTS
   ------------------------------------------------------------
   These records show Participants entering events and
   selecting a category.
   The status is also stored so that the progress of each
   enrolment can be tracked.
   ============================================================ */

INSERT INTO Enrolments
    (ParticipantID, EventID, CategoryID,
     EnrolmentStatusID, EnrolmentDate)
VALUES
    (3, 1, 1, 2, '2026-09-01'),

    (4, 1, 2, 2, '2026-09-02'),

    (5, 2, 3, 2, '2026-09-03'),

    (3, 3, 5, 1, '2026-09-04'),

    (4, 3, 6, 2, '2026-09-05');
GO


/* 16. ADD SAMPLE RESULTS
   ------------------------------------------------------------
   Results are linked to completed enrolments.
   FinishTime stores how long the Participant took, while
   FinishPosition stores their finishing position.
   ============================================================ */

INSERT INTO Results
    (EnrolmentID, FinishTime, FinishPosition)
VALUES
    (1, '01:02:35', 15),

    (2, '01:15:42', 28),

    (3, '02:10:18', 11);
GO


/* 17. CHECK THE DATA IN EACH TABLE
   ------------------------------------------------------------
   These simple queries allow us to confirm that the records
   were inserted correctly.
   ============================================================ */

SELECT * FROM Users;

SELECT * FROM EventTypes;

SELECT * FROM Events;

SELECT * FROM Categories;

SELECT * FROM EnrolmentStatuses;

SELECT * FROM Enrolments;

SELECT * FROM Results;
GO


/* 18. DATABASE RELATIONSHIP TEST 1
   ------------------------------------------------------------
   This query joins Participants, Enrolments, Events,
   Categories and Enrolment Statuses.
   ============================================================ */

SELECT
    u.FirstName + ' ' + u.LastName AS ParticipantName,
    e.EventName,
    c.CategoryName,
    es.StatusName,
    en.EnrolmentDate
FROM Enrolments en
INNER JOIN Users u
    ON en.ParticipantID = u.UserID
INNER JOIN Events e
    ON en.EventID = e.EventID
INNER JOIN Categories c
    ON en.CategoryID = c.CategoryID
INNER JOIN EnrolmentStatuses es
    ON en.EnrolmentStatusID = es.EnrolmentStatusID
ORDER BY en.EnrolmentID;
GO


/* ============================================================
   19. DATABASE RELATIONSHIP TEST 2
   ------------------------------------------------------------
   This query checks that every event is connected to the
   correct Organiser and Event Type.
   ============================================================ */

SELECT
    e.EventID,
    e.EventName,
    u.FirstName + ' ' + u.LastName AS OrganiserName,
    e.EventDate,
    e.Location,
    e.Distance,
    et.TypeName AS EventType
FROM Events e
INNER JOIN Users u
    ON e.OrganiserID = u.UserID
INNER JOIN EventTypes et
    ON e.EventTypeID = et.EventTypeID
ORDER BY e.EventID;
GO


/* ============================================================
   20. DATABASE RELATIONSHIP TEST 3
   ------------------------------------------------------------
   This query checks that each Category belongs to the
   correct Event.
   ============================================================ */

SELECT
    e.EventName,
    c.CategoryID,
    c.CategoryName,
    c.Description
FROM Categories c
INNER JOIN Events e
    ON c.EventID = e.EventID
ORDER BY e.EventID, c.CategoryID;
GO


/* ============================================================
   21. DATABASE RELATIONSHIP TEST 4
   ------------------------------------------------------------
   This final query follows a result back to the Participant
   and Event through the Enrolment table.

   This confirms that the Results relationship is working.
   ============================================================ */

SELECT
    u.FirstName + ' ' + u.LastName AS ParticipantName,
    e.EventName,
    r.FinishTime,
    r.FinishPosition
FROM Results r
INNER JOIN Enrolments en
    ON r.EnrolmentID = en.EnrolmentID
INNER JOIN Users u
    ON en.ParticipantID = u.UserID
INNER JOIN Events e
    ON en.EventID = e.EventID
ORDER BY r.FinishPosition;
GO


/* ============================================================
   END OF RACEDAY DATABASE SCRIPT
   ============================================================ */