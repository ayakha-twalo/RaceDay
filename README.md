# PROG6212 — Programming 2B

## Student Information

**Name and Surname:** Ayakha Twalo  
**Student Number:** ST10490430  
**Module:** Programming 2B  
**Project:** RaceDay Event Management System  

---

# RaceDay Event Management System

## Project Overview

RaceDay is an event management system designed to make it easier for
Organisers to create and manage events while allowing Participants to browse
events, enrol in events and view their results.

The system is being planned as a web-based application with a relational
database and a REST API.

Part 1 focuses on planning and designing the system before development begins
in Part 2 and Part 3.

The main areas planned for the RaceDay system are:

- User registration and login
- User profile management
- Event management
- Event categories
- Participant enrolments
- Race results
- Role-based access for Organisers and Participants

## User Roles

### Organiser

Organisers are responsible for managing RaceDay events.

An Organiser will be able to:

- Create new events
- View events
- Update existing events
- Delete events
- Create categories for events
- View enrolments for their events
- Capture Participant results
- Manage event information

Organisers have access to management functions that are not available to
Participants.


### Participant

Participants use RaceDay to find and take part in events.

A Participant will be able to:

- Register for a RaceDay account
- Log in to the system
- View and update their own profile
- Browse available events
- View event details
- View event categories
- Enrol in an event
- Select a category when enrolling
- View their own enrolments
- View their own race results

Participants do not have access to Organiser-only management functions.


## Part 1

Part 1 focuses on planning the RaceDay system and creating the database
structure that will support the later development of the application.

The three main technical deliverables are:

### ERD

The Entity Relationship Diagram shows the data that will be stored by the
RaceDay system and how the entities are related.

The ERD contains the following entities:

- User
- Event
- EventType
- Category
- Enrolment
- EnrolmentStatus
- Result

The ERD identifies primary keys, foreign keys, relationships and cardinality.

The completed ERD is available in the `/docs` folder as:

`RaceDay_ERD.pdf`


### API Endpoint Plan

The API Endpoint Plan describes the REST API that will be developed in a
later part of the project.

The plan covers:

- Authentication
- User Profile
- Events
- Categories
- Event Enrolments
- Results

Each endpoint is documented using:

- HTTP Method
- Route
- Description
- Role Required
- Request Body
- Expected Response

The completed API Endpoint Plan is available in the `/docs` folder as:

`RaceDay_API_Endpoint_Plan.pdf`


### SQL Database Script

The SQL Database Script implements the database design from the ERD using
Microsoft SQL Server.

The database contains the following tables:

- Users
- EventTypes
- Events
- Categories
- EnrolmentStatuses
- Enrolments
- Results

The database includes:

- Primary keys
- Foreign keys
- NOT NULL constraints
- UNIQUE constraints
- CHECK constraints
- Default values
- Sample data
- Relationship testing queries

The SQL script was tested using SQL Server Management Studio (SSMS) and
executes successfully.

The completed SQL script is available in the `/docs` folder as:

`RaceDayDB.sql`


## Repository Structure

The repository is organised so that the Part 1 planning documents are kept
together in the `/docs` folder.

The planned repository structure is:

```text
RaceDay
│
├── .github
│   └── workflows
│       └── part1-ci.yml
│
├── docs
│   ├── RaceDay_ERD.pdf
│   ├── RaceDay_API_Endpoint_Plan.pdf
│   └── RaceDayDB.sql
│
└── README.md