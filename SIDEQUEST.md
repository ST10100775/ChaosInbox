# Side Quest —  LM Studio vs Ollama

As part of the Chaos Inbox build, I spent approximately 45 minutes exploring LM Studio as an alternative local-model runtime to Ollama, which is the runtime currently used by Chaos Inbox.

The purpose of this side quest was not to rebuild the project with a different runtime, but to challenge my original technical decision and determine whether Ollama was still the better fit.

## What Did I Try?

I installed LM Studio and explored its local AI workflow, including:

  - Browsing the available model catalogue
  - Downloading a small model capable of running locally
  - Testing prompts through LM Studio's chat interface
  - Exploring model configuration and parameters
  - Looking at how its local API server could be connected to an application

This gave me enough exposure to compare its development experience with the Ollama-based workflow already used by Chaos Inbox.

## What Problem Does It Solve Differently?

Both Ollama and LM Studio allow language models to run locally rather than requiring Chaos Inbox to depend entirely on a hosted AI API.

The biggest difference I noticed was how they expose that local-model experience.

| Ollama |	LM Studio |
|---|---|
| Developer and terminal focused |	Visual and GUI focused |
| Simple CLI workflow	| Built-in model browser |
| Easy HTTP API integration |	Interactive prompt testing |
| Fits naturally behind an application |	Useful for model experimentation |
| Minimal runtime interface	| Easier model inspection and configuration |

### Ollama

Ollama feels like an infrastructure component.

I can pull a model, start it and communicate with it through an HTTP API with relatively little setup. This fits Chaos Inbox because the model runtime exists behind the application rather than being part of its user interface.

### LM Studio

LM Studio feels more like a local AI experimentation environment.

Its graphical interface makes it easy to browse models, inspect their information, experiment with prompts and modify parameters before connecting anything to application code.

## What Felt Better?

The strongest advantage of LM Studio was visibility.

I could browse and test models without immediately needing terminal commands or manually constructed API requests. When experimenting with an unfamiliar model, this reduced the friction between downloading it and understanding how it behaves.

LM Studio would be particularly useful when comparing several models based on:

  - Output quality
  - Context size
  - Response behaviour
  - Model size
  - Configuration options
  - Hardware requirements

For the initial model exploration stage, I would probably choose LM Studio.

## What Felt Worse?

For Chaos Inbox specifically, the additional graphical interface did not solve an important problem.

Chaos Inbox mainly needs a predictable local endpoint that can:

Raw Email
   ↓
Local LLM
   ↓
Structured Ticket Information
   ↓
Chaos Inbox Pipeline

  - I also wanted the runtime to be easy to reproduce from the project's setup instructions.

  - Ollama's simple command-line workflow fits this requirement better for me and keeps the AI runtime separate from the Chaos Inbox application itself.

  - More importantly, changing runtimes would not eliminate the main reliability problems associated with using an LLM.

Regardless of whether Chaos Inbox uses Ollama or LM Studio, the application still needs to handle situations where the model:

  - Is unavailable
  - Times out
  - Returns malformed output
  - Produces an unexpected response structure
  - Confidently produces an incorrect classification

This reinforced an important lesson from the project:

Changing the model runtime does not remove the need for defensive application design.

## Would I Switch Chaos Inbox to LM Studio?

No — not for the current version.

LM Studio is a tool I would use for model exploration and comparison, but I would keep Ollama as the runtime behind Chaos Inbox.

The project is intentionally small, and switching runtimes at this stage would introduce additional integration work without meaningfully improving the ticket-processing pipeline.

## If I had another week, however, I would consider making the AI layer provider-independent.

For example:

                    ┌── Ollama
Chaos Inbox ── AI Provider Layer
                    └── LM Studio

I could then run the same evaluation dataset through both configurations and compare measurable results such as:

  - Metric	Ollama	LM Studio
  - Priority classification accuracy
  - Deadline extraction accuracy
  - Average response time
  - Invalid-output rate
  - Resource usage

That would allow the runtime and model to be selected based on measured behaviour rather than developer preference alone.

## What I Learned

The side quest did not convince me to replace Ollama, but it changed how I would approach model selection in a larger version of Chaos Inbox.

My revised process would be:

Explore → Compare → Measure → Integrate

Experiment visually first, measure the candidates, and only then choose the runtime and model used by the application.

For Chaos Inbox today, Ollama remains the better runtime choice because it provides the simple, reproducible and application-focused local AI workflow that the project needs.
