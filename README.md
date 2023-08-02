# Thing Runner (things)

Thing Runner (`things`) is a lighweight binary that does exactly what
it sounds like: it runs things (as services).

I was frustrated that every time I spun up a silly little app it took
what felt like an _eternity_ to get it deployed somewhere on resources
I was already paying for (a VPS). I wanted something that was one
command, no matter what I was doing, to update and start the service.
And more importantly, always **the same** command. So whether I'm
running a .NET app or a PHP app, it's always just `things start <name>`.

Furthermore, I want these services to be integrated into the system's
service management, so I can do `/etc/init.d/<service> start` (or
eventually `launchctl start <service>`) and have the service start,
without having to write LSB-compliant run scripts or look up launchd's
configuration syntax for the umpteenth time.

I also wanted the configuration to be dead simple. I still can't wrap
my head around kubernetes because I don't understand why I need 16
different files to define a single service. I also don't understand
why I need a command to run locally that is really just a glorified
API client. Which brings me to the final piece of inspiration.

I've been using Amazon's ECS a lot lately. It has a lot of good aspects,
but it has two main things working against it:

1.  It's AWS, so if you don't have the requisite PhD in AWS, your
    chances of having the steps that worked the last time you used it
    work again are about as good as flipping a coin.
2.  It can only run docker/OCI containers. This is great if everything
    you want to run is already in containers, but otherwise you have to
    add another non-trivial build step.
    That being said, the design philosophy around ECS is pretty snazzy --
    you define a service made up of one or more tasks and you can poke at
    the UI or issue API calls to configure, start, stop, and update a
    service.

So that's what I did.

## Quick Start

1.  Build the tool (instructions below)

2.  Install the binary (`things`) in your PATH

3.  Configure a service in `/etc/things/your-service.json` (with the
    instructions below).

4.  Start the service by running `things start your-service`.

## Build

Thing Runner requires .NET 7 to build, but no runtime to run.

One command does it all:

```
dotnet publish -c Release -r linux-x64
```

This will put the binary in `bin/Release/net7.0/linux-x64/publish/things`

## Install

### Install the CLI

Just stick it in your path. `/usr/bin` is recommended, but if your
configuration is non-standard, pick a directory that is in PATH for
both your user AND `root`.

```
mv things /usr/bin
```

### Install the REST Server

If you want to use the REST server, you can use `things` to install it,
as a `things` service, and then start it:

```
things install-server
things start things-server
```

NOTE: Currently, this installs and runs as root, which is less than desirable.
This is at the top of the list to figure out.

## Configure

A service configuration looks something like this:

```
{
  "name": "sample",
  "description": "A sample that shows how to configure things",
  "tasks": [
    {
      "name": "app",
      "type": "docker",
      "env": {
        "SETTING_ONE": "value"
      },
      "image": "crccheck/hello-world:latest",
      "ports": ["8080:8000"],
      "opts": ["--add-host=host.docker.internal:host-gateway"]
    }
  ]
}
```

The name is a "display name"; the name used to reference the service in all
commands and API requests is the name of the file (e.g. if this is in a file
called `sample.json`, the service can be started with `things start sample`).

Description isn't currently used anywhere, but it's helpful to document what
you're doing.

Tasks is an array of task configurations. Each task is started individually
and can have different types. That means you could potentially have a docker
container and a local script that together make up a service.

All tasks have a `name` (which is used when creating some incidentals, like
naming a docker container or PID file), a `type` (which determines what to
do to actually start/stop/update the task), and one or more keys in an `env`
object (each of which sets a corresponding environment variable when starting
the task)

Docker tasks have settings you might expect from a docker run command. Note
that this is NOT exhaustive. `opts` is your friend if something doesn't have
its own task config key (for instance, there's no `volumes` key).

Script tasks have settings `start-command` (exactly what it sounds like),
`runas` (which isn't supported yet but will take a username to run as), and
`daemonize` (which takes either `true` or `false`). If the task is set to
daemonize, the process is left running and its PID is saved. Stopping the
task kills the specific PID. If not daemonized, `things` waits for the
start command to finish. Stopping such a task is not yet implemented.

## Start, stop, and update services

### `things start <service>`

Starts the requested service

### `things stop <service>`

Stops the requested service

### `things update <service>`

Updates the service. Each type of task updates a little differently.

For instance, a `docker` task pulls the latest version of the image, stops
and removes the running container, and starts a new one. This also takes into
account any changes to environment variables or other config.

Script tasks dont support update, but don't fail the update process (so if
you have one docker task and one script task, everything will still succeed).

If we add something like a "git" task type, update would do something like
pulling the git repository and restarting the app inside.

## Contributing

I have ideas of what I would want to add, but at the moment I don't _need_ to
add anything more. If I have time I'll add more just for fun, but I'm curious
what the community wants.

If you want to see something added, submit a PR or open an issue.

If you find a bug or have a concern, open an issue.

If you just like the project and want to show your support, give the project
a star and/or hit us up wherever we can be found. (If the community gets big
enough, maybe we'll start a Slack organization or something.)

## NOTES

- The intention is for this to be portable, but a lot currently assumes \*nix.
- Only "docker" and "script" task types are supported, but I want to add more.
- There's a LOT of opportunity for extension, but even as it is, it's viable.
