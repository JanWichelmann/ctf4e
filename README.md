[![Build](https://img.shields.io/github/workflow/status/JanWichelmann/ctf4e/Build)](https://github.com/JanWichelmann/ctf4e/actions?query=workflow%3ABuild) [![Ctf4e.Api NuGet package](https://img.shields.io/nuget/v/Ctf4e.Api?label=nuget)](https://www.nuget.org/packages/Ctf4e.Api/)


# CTF4E: Capture the Flag for Education

CTF4E is a framework for practical courses, which combines learning goals with gamification. It is being developed in the context of the [IT security courses at the University of Lübeck](https://www.its.uni-luebeck.de/en/).

Features:
- Division of a course into a number of _labs_, which each consist of a set of _exercises_ and _flags_
- Mandatory and optional exercises per lab
- Solving an exercise grants points; wrong submissions may impose penalty points
- Flags are special strings, designed to be hidden and/or hard to acquire; flag points scale with the amount of finds
- Partitioning of students into groups, which share points and, if desired, grades
- Fine-granular control over working times, exercise and flag points
- Scoreboard with rankings per lab and for the entire course
- API for connecting to _lab servers_

## Ctf4e.Server
[![Docker Image Version (latest by date)](https://img.shields.io/docker/v/ctf4e/ctf4e-server?label=docker&sort=date)](https://hub.docker.com/r/ctf4e/ctf4e-server/tags)

This is the main application, which keeps track of exercise results, flag submissions and the scoreboard. It links to the corresponding lab servers.

### Quick start

A new instance of the CTF server can be created by following these steps:
1. Pull the files from the `run/server/` folder.
2. Edit `config.json` and `.env` to match the corresponding runtime environment.
3. `docker-compose up -d` will start the CTF server.

Finally, open a browser and navigate to the specified host/port.

## Ctf4e.LabServer
[![Docker Image Version (latest by date)](https://img.shields.io/docker/v/ctf4e/ctf4e-labserver?label=docker&sort=date)](https://hub.docker.com/r/ctf4e/ctf4e-labserver/tags)

This is a generic lab server implementation, suitable for most common tasks. A lab server offers an interface for entering exercise solutions, and sends its verdicts ("passed"/"not passed") to the CTF server.

Features:
- Integrates with the CTF server
- Keeps track of lab exercises
- Each exercise is either connected to a corresponding exercise entry on the CTF server, or produces a flag code
- Each exercise has a title, a description (optional) and a link (optional)
- String exercises: List of possible solution strings for each exercise, in key/value format
	- Exercises may either require a specific solution (fixed, random key for each user) or an arbitrary one
- Multiple choice exercises: List of correct and wrong solutions, where one or all correct solutions need to be selected
- Script exercises: Triggers a script in an associated Docker container, allowing for arbitrary checks; also supports optional string input

### Quick start

The folder `run/labserver/` contains an example `docker-compose` configuration for setting up a CTF server combined with a single lab server instance.
1. Pull the files from the `run/labserver/` folder.
2. Edit `config.json`, `config-labserver1.json` and `.env` to match the corresponding runtime environment. The lab server configuration file format is documented in [`docs/labserver-config.md`](docs/labserver-config.md).
3. `docker-compose up -d` will start the CTF server and a lab server.

Finally, open a browser and navigate to the specified host/port.

## Contributing

Contributions are appreciated! Feel free to submit issues and pull requests.

## License

The entire system is licensed under the MIT license. For further information refer to the LICENSE file.
