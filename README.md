![Build](https://github.com/JanWichelmann/ctf4e/workflows/Build/badge.svg)

**NOTE: This is still a work in progress and needs a few more adjustments before being ready for use in production.**

# CTF4E: Capture the Flag competitions for education

CTF4E is a framework for practical courses, with focus on combining learning goals with gamification. It has been designed in the context of the [IT security courses at the University of Lübeck](https://www.its.uni-luebeck.de/en/), and is still being actively developed.

Features:
- A pratical course is represented by a number of _labs_, which each consist of a set of _exercises_ and _flags_.
- Each course can have several mandatory and optional exercises. Solving an exercise grants a certain number of points; wrong submissions may impose penalty points.
- Flags are special strings, designed to be hidden and/or hard to acquire. Flag points scale with the amount of finds.
- Students can divide themselves into groups, which share points and, if desired, grades.
- A scoreboard shows the rankings per lab and for the entire course.

## Ctf4e.Server

This is the main application, which keeps track of exercise results, flag submissions and the scoreboard. It links to the single lab servers.

### Quick start

A new instance of the CTF server can be created by following these steps:
1. Pull the files from the `run/server/` folder.
2. Edit `config.json` and `.env` to match the corresponding runtime environment.
3. `docker-compose up` will start the CTF server.

Finally, open a browser and navigate to the specified host/port.

## Ctf4e.LabServer

WIP

## Contributing

Contributions are appreciated! Feel free to submit issues and pull requests.

## License

The entire system is licensed under the MIT license. For further information refer to the LICENSE file.
