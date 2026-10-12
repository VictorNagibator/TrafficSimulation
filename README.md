# Traffic Simulation

A small microsimulation of city traffic, written in C# with WPF.

You draw a city out of roads and junctions, drop cars onto it, press **Start**, and watch how
traffic behaves: cars accelerate, keep their distance, change lanes, queue at traffic lights, give
way to traffic coming from the right, wear the asphalt down, crash in the rain and get stuck in
jams. Roads can be edited while the simulation is stopped — the model keeps itself consistent.

---

## Features

| Area | What it does |
| --- | --- |
| Road network | Two-way and one-way roads, 1–10 lanes per direction, named roads, junctions created by clicking or by splitting an existing road |
| Driving | Per-tick acceleration, speed limited by the road's quality, the speed limit and the weather |
| Car following | Cars keep a safety distance, and pull out around a car that has broken down when there is room |
| Lane discipline | Lane changes with a gap check; turn permissions depend on the lane (the rightmost lane may turn right, the leftmost may turn left or make a U-turn) |
| Junctions | Right-of-way rule for uncontrolled junctions; traffic lights per (road, junction) with configurable red and green phases |
| Traffic lights | Red → yellow → green cycle, shared across the network, editable per road |
| Environment | Random rain periods that slow traffic down and make accidents more likely |
| Breakdowns and accidents | Disabled vehicles block their lane for a configurable time |
| Road wear | Every crossing degrades the road a little; a worse road lowers the speed and raises the accident rate |
| Visualisation | Zoom, pan, traffic-density overlay, animated rain, speed-limit signs, one-way arrows, scale bar |
| Editing | Live road editor: change the type, lane count, speed limit, quality or name of any road; delete roads; split roads; clear the map |

---

## Getting started

### Requirements

- The **.NET 8 SDK**
- **Windows** to run the application, because it is a WPF desktop application
  (the engine and its tests build and run on any platform)

### Build and run

```bash
git clone https://github.com/VictorNagibator/TrafficSimulation.git
cd TrafficSimulation

dotnet build TrafficSimulation.sln -c Release

# Run the desktop application (Windows only)
dotnet run --project src/TrafficSimulation.App
```

### Run the tests

```bash
# Engine tests: platform independent, run everywhere
dotnet test tests/TrafficSimulation.Engine.Tests

# Presentation layer tests: Windows only
dotnet test tests/TrafficSimulation.App.Tests

# Everything
dotnet test TrafficSimulation.sln
```

---

## Using the simulator

The window is split into a toolbar on top and the map below.

| Control | Action |
| --- | --- |
| **City settings** | Speed limit, road quality, accident/breakdown probability and duration, rain probability and duration for the whole city |
| **Start / Stop simulation** | Runs or pauses the simulation loop |
| **Add vehicle** | Spawns a car at a random road, lane and direction |
| **Remove all vehicles** | Empties the city of cars but keeps the roads |
| **Add road** | Starts the road tool: click once to place the first junction, move the mouse to preview, click again to finish. Clicking the *middle* of an existing road splits it and continues from the new junction |
| **Cancel** | Leaves the road tool |
| **Clear map** | Deletes every road and vehicle |
| **↺ / + / −** | Reset the view, zoom in, zoom out (the mouse wheel works too) |
| **Traffic density button** | Colours every road by how full it is: green, yellow, red, dark red |

**Clicking** a road, a junction or a car opens it:

- a road opens its settings window (name, speed limit, quality, one-way/two-way, lanes, delete);
- a junction opens its close-up, where you select one of its roads and give it a traffic light with
  a red and a green phase, or force the current signal;
- a car asks whether you want to remove it.

**Dragging** empty space pans the map. The simulation pauses automatically while a settings window
is open, so you cannot edit a road that a car is driving on.

---

## Architecture

The solution is layered, and the dependency arrows all point inwards: the simulation knows nothing
about the user interface, and the user interface knows nothing about how the simulation is
implemented.

```
┌──────────────────────────────────────────────────────────────┐
│ TrafficSimulation.App                    (net8.0-windows)    │
│ WPF windows, canvas rendering, mouse interaction, viewport   │
└───────────────────────────┬──────────────────────────────────┘
                            │ references
┌───────────────────────────▼──────────────────────────────────┐
│ TrafficSimulation.Engine                 (net8.0)            │
│ Domain model, simulation loop, map editing. No UI types.     │
└──────────────────────────────────────────────────────────────┘
```

| Project | Target | Responsibility |
| --- | --- | --- |
| `TrafficSimulation.Engine` | `net8.0` | The whole simulation: geometry, domain model, tick loop, map editing. No WPF, no pixels, no `System.Windows` |
| `TrafficSimulation.App` | `net8.0-windows` | WPF windows, rendering to `Canvas`, mouse input, zoom/pan, settings dialogs |
| `TrafficSimulation.Engine.Tests` | `net8.0` | Unit tests for the engine. Run on any platform |
| `TrafficSimulation.App.Tests` | `net8.0-windows` | Unit tests for the viewport and the input controllers. Windows only |

### Why the engine is a separate assembly

Two reasons, and both of them are practical rather than aesthetic.

1. **It can be tested.** The interesting behaviour of this program — car following, right of way,
   traffic light timing, road wear — has nothing to do with WPF. Keeping it in a plain class library
   means `dotnet test` covers it in a second, with no window, no dispatcher and no display.
2. **It cannot accidentally depend on the view.** A WPF project cannot be referenced from a
   `net8.0` library, so the compiler enforces the boundary. Every attempt to sneak a
   `System.Windows.Point` into the model becomes a build error.

### Layer by layer

**Engine**

- `Geometry` — `Vector2`, `LineSegment` and `Angles`. The engine uses its own two-dimensional value
  types instead of WPF's, which is what makes it portable.
- `Randomness` — `IRandomSource` and its implementations. Every random decision in the simulation
  goes through this interface, which is what makes a run reproducible.
- `Domain` — the model: `City`, `Road`, `Intersection`, `Vehicle`, `TrafficLight` and the
  `IntersectionIndex` that guarantees one junction per position.
- `Simulation` — the surrounding machinery: `CitySettings`, `VehicleBehaviourSettings`,
  `SimulationContext`, the traffic light controller, the map editor and the change notifier.

**App**

- `Rendering` — `MapViewport` (metres ↔ pixels, zoom, pan), `CityMapRenderer` (draws a city onto a
  canvas) and `IntersectionPreviewRenderer` (the junction close-up).
- `Interaction` — `MapInputController` (the mouse state machine: panning, placing roads, splitting
  roads) and `TrafficLightEditor` (the rules of the traffic-light panel).
- The windows themselves contain only event plumbing.

---

## How the simulation works

### One tick

The simulation advances in fixed steps of `SimulationConstants.TickSeconds` (0.1 s), driven by a
`DispatcherTimer`.

Every tick happens in two clearly separated phases:

1. **The world changes.** Weather is advanced, then every traffic light ticks.
2. **The vehicles react.** A single `SimulationContext` describing the world is built, and every
   vehicle is updated against it.

Because the context is built once and never mutated during phase 2, vehicles can never observe each
other half-updated. That is what makes a given seed produce exactly the same run, tick after tick —
a property the tests rely on.

### The order of a vehicle's decisions

A car decides whether to move in a fixed order that mirrors real driving priorities. The first rule
that applies wins:

1. if the car is already crashed or broken down, it counts down its timer and does nothing;
2. stop at a red or yellow light before the stop line;
3. stop for the car in front — unless that car is disabled and there is room to pull out;
4. give way to traffic approaching the same junction from the right;
5. stop if the road ahead has no free lane;
6. stop and turn around at a dead end.

Only if none of these applies does the car accelerate, move, maybe change lane, and — if it has
crossed the end of its road — move onto the road it had chosen.

### Speeds, distances and units

All distances in the engine are **metres** and all speeds are **kilometres per hour**. The unit
conversions live in one place each:

- metres per tick: `SpeedKph / 3.6 * TickSeconds`
- acceleration gain per tick: `AccelerationKphPerTick`, which is 1 km/h per tick by default, that is
  about 2.8 m/s²

The maximum speed a car is willing to reach is

```
min(speedKph + acceleration, quality × speedLimit − rainPenalty)
```

so a road in poor condition both lowers the ceiling and raises the chance of an accident.

### Lanes and turns

Lanes are numbered `1 … LanesPerDirection`, always counted from the **right** hand side of the
direction of travel. Lane `1` is therefore the rightmost lane both for a car driving forward and
for one driving backward, which is what makes the turn rules lane-based instead of
direction-based:

- the rightmost lane may go straight on or turn **right**;
- the leftmost lane may go straight on or turn **left**, and may make a U-turn on a two-way road;
- on a single-lane road, any turn is allowed.

### Junctions and directions

A junction classifies a heading into *left*, *forward* or *right* by comparing angles, using
`SimulationConstants.AngleToleranceRadians` (10°). The screen convention applies: X points right,
Y points **down**, and angles grow clockwise, so a positive turn is a turn to the driver's left.

### Traffic lights

A light is identified by the pair (road, junction). A one-way road only ever has one possible
approach direction, but a two-way road is controlled in both directions by the same light.
The cycle runs red → yellow → green → red; only red and green are configurable, and the yellow phase
is fixed at two seconds.

---

## Project layout

```
TrafficSimulation/
├── TrafficSimulation.sln
├── Directory.Build.props            # settings shared by every project
├── README.md
├── src/
│   ├── TrafficSimulation.Engine/    # the simulation, platform independent
│   │   ├── SimulationConstants.cs
│   │   ├── Geometry/                # Vector2, LineSegment, Angles
│   │   ├── Randomness/              # IRandomSource and implementations
│   │   ├── Domain/                  # City, Road, Intersection, Vehicle, TrafficLight
│   │   └── Simulation/              # settings, context, light controller, map editor
│   └── TrafficSimulation.App/       # the WPF application
│       ├── MainWindow.xaml(.cs)
│       ├── *SettingsWindow.xaml(.cs)
│       ├── Rendering/               # viewport, map renderer, junction preview
│       ├── Interaction/             # mouse state machine, traffic light editor
│       └── Media/                   # icons and the animated rain image
└── tests/
    ├── TrafficSimulation.Engine.Tests/
    └── TrafficSimulation.App.Tests/
```

---

## Tests

The suite is split along the same line as the production code.

| Project | Framework | Platform | Covers |
| --- | --- | --- | --- |
| `TrafficSimulation.Engine.Tests` | xUnit | any | Geometry, road and junction behaviour, turn rules, traffic lights, vehicle decisions, the tick loop, map editing |
| `TrafficSimulation.App.Tests` | xUnit | Windows | The metres↔pixels viewport, zoom and pan limits, and the traffic-light editor |

Two things make the engine tests unusually easy to write, and they are worth calling out because
they are the direct result of the refactoring:

- **`IRandomSource`** is injected everywhere, so tests can queue the exact random values a decision
  needs (`TestCity.ThenRandomDouble`) or remove randomness entirely. No test has to loop until
  something happens.
- **`SimulationContext`** can be built by hand, so a single vehicle can be exercised against a
  controlled world in three lines.

Test helpers live in `tests/TrafficSimulation.Engine.Tests/Helpers`:

- `TestCity` builds a deterministic city and places vehicles at exact positions;
- `RoadLayouts` provides the common shapes (a four-way crossing and a T junction);
- `QueuedRandomSource` returns the values a test queued, then falls back to a seeded generator.

---

## Known limitations

- A WPF application, so it runs on Windows only. The engine and its tests are portable.
- Roads are straight segments; there are no curves, roundabouts or slip roads.
- Two roads never cross without a junction: the editor refuses to build a road that cuts through an
  existing one.
- The right-of-way rule is a simplified "give way to the right"; there is no priority road sign and
  no left-turn conflict handling.
- Vehicles have no destination: a car picks a random legal turn at each junction.
- The simulation is single-threaded and redraws the whole canvas every tick, which is fine for a few
  hundred vehicles but does not scale beyond that.
- Traffic lights are per (road, junction) rather than being grouped into coordinated phases, so a
  junction's lights can show conflicting signals.
- There is no save/load; the city exists only for the lifetime of the process.