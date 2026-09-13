# Build Log

This log records how Chaos Inbox changed while I was building it, including decisions, failures, AI assistance and things I still do not completely understand.

---

## Day 1 — Skeleton and Persistence

### What I attempted

I started by creating the basic Chaos Inbox structure in Visual Studio using ASP.NET Core. My first goal was deliberately small: get an application running, create tickets, store them in SQLite and display them through a simple dashboard.

The initial version included:

* ASP.NET Core backend
* SQLite persistence with Entity Framework Core
* Basic ticket model
* Static web dashboard
* API endpoints for retrieving and updating tickets

I deliberately avoided starting with Gmail or the AI model because I wanted a working application before introducing external dependencies.

### What broke

The first challenge was getting all of the project dependencies and configuration to agree with each other. I also had to think differently about persistence once Docker was introduced. A SQLite database stored inside a disposable container is not really persistent if deleting the container also deletes the database.

This forced me to separate the application's database location from the container itself and use a persistent Docker volume for the containerised version.

Another small challenge was keeping the frontend and API simple. I initially considered a separate frontend framework, but realised that it would add another build system and more failure points without helping me prove the main idea.

### What I learned

The biggest lesson from the first stage was that persistence is not simply "I used SQLite." I had to think about where the database actually lives and what happens when the process or container disappears.

I also learned that reducing architecture can be an engineering decision rather than a shortcut. Serving a small dashboard directly from ASP.NET Core gave me fewer moving pieces and more time to work on the unusual parts of Chaos Inbox.

---

## Day 2 — Email Normalisation

### What I attempted

The next step was turning emails into a common format before creating tickets.

Chaos Inbox can receive email information from:

* Built-in demo emails
* `.eml` files
* Gmail

Instead of allowing every source to create tickets differently, I created a common email-envelope representation.

The pipeline became:

`Email source → EmailEnvelope → TicketProcessor → Ticket`

### Why

I did not want ticket analysis coupled to Gmail.

If Gmail-specific objects were passed directly into the ticket analyser, adding another source such as Outlook would require changing the analysis logic. Normalising first means the ticket processor does not need to know whether an email came from Gmail, a file or a test fixture.

This also gave me a reliable demo mode. Falcorp can run the application without access to my Google account or OAuth credentials.

### Unexpected problem

Email is much messier than I initially assumed.

A message is not guaranteed to contain one convenient plain-text body. MIME messages can contain different parts, and Gmail also represents messages using nested payloads. That meant "read the email body" was not one simple string operation.

I had to treat email parsing as its own boundary rather than assuming that every provider would return Subject, From and Body in exactly the format I wanted.

The result was another useful architectural boundary:

`Provider-specific email → normalised email → provider-independent processing`

### What I learned

This was my first clear example of why abstraction matters in a small project. Creating `EmailEnvelope` initially felt like an extra layer, but it made the rest of the system much easier to reason about.

It also means that adding another source later should mostly require a new adapter rather than rewriting the ticket-processing logic.

---

## Day 3 — AI Triage

### What I attempted

I added the AI triage stage responsible for turning an email into useful ticket information.

The goal was not just to summarise an email. I wanted the system to extract information that could actually help someone work through an inbox:

* A short summary
* Priority
* Deadline where one could be identified
* Confidence
* Information that could be displayed as a ticket

I used a local model through Ollama so the AI component could run without introducing a new paid dependency.

### Where AI sped me up

AI was most useful as a development assistant while I was working with unfamiliar parts of the project.

I used it to help:

* Understand unfamiliar API and library concepts
* Explore approaches for Gmail OAuth and MIME parsing
* Reason about the ticket-processing architecture
* Explain compiler and configuration errors
* Design the structured response expected from the local model
* Think of edge cases for priority and deadline extraction

It was especially useful for quickly moving from "I do not know this API" to "I know enough terminology to investigate it properly."

### How I checked AI-generated suggestions

I did not use AI output as the final authority. Depending on the problem, I checked generated suggestions against:

* Visual Studio compiler errors
* Package/API documentation
* Actual HTTP responses
* Application behaviour
* Automated tests

The compiler was particularly useful because plausible-looking C# is still wrong if the API being called does not actually have the method or signature the model claimed.

### What I learned

The biggest lesson was that putting an LLM inside an application is different from chatting with one.

The application needs predictable data. A response that sounds intelligent but does not match the expected JSON structure is still a failed response.

That pushed me towards structured output and explicit validation instead of accepting whatever text the model generated.

---

## Day 4 — Failure Injection

Once normal processing worked, I deliberately started breaking it.

This was where Chaos Inbox became more than an email organiser.

### Ollama unavailable

I stopped the local model and processed another email.

The first design would have made AI availability equivalent to application availability. I did not want that.

I changed the pipeline so that:

`Ollama → validation → fallback analyser`

If Ollama is unavailable or its response cannot be trusted, the deterministic analyser still creates a ticket.

The resulting ticket records that fallback processing was used rather than pretending that the AI succeeded.

### Broken summaries

I also experimented with deliberately removing a generated summary.

That gave me a repeatable way of seeing what the application did when stored ticket information was incomplete.

It made me realise that validation should not only happen when information first enters the system. Data that was valid initially can become invalid later because of bugs, migrations or other changes.

### Fake upstream failure

I added a simulated upstream failure to represent an external service becoming unavailable.

Instead of deleting information that had already been processed, Chaos Inbox records the failure and keeps the existing ticket.

That led to one of the project's main design rules:

> A dependency failing should not mean losing information the system already has.

### What I learned

Failure injection gave me a much better way to test the system than waiting for something to fail naturally.

It also made the project more interesting to me because I was no longer only designing the happy path.

---

## Day 5 — Priority, Deadlines and Ticket Workflow

### What I attempted

The next problem was making the generated tickets useful instead of simply displaying AI summaries.

I added:

* `Critical`, `High`, `Medium` and `Low` priorities
* Deadline extraction
* Overdue detection
* Ticket statuses
* Sorting based on priority and deadline

The status workflow became:

`New → In Progress → Waiting → Completed → Archived`

Users can manually update a ticket once they have dealt with the email.

### What became harder than expected

Priority sounds simple until two signals disagree.

For example, an email might sound fairly normal but contain:

> "Please have this completed by tomorrow."

Another email might contain the word "urgent" but have no actual deadline.

I did not want one keyword to completely determine the result.

The fallback analyser therefore uses multiple signals, while the local model gets the wider context of the email.

I also had to decide how tickets should be sorted when priority and deadlines conflict.

I chose to keep unfinished tickets ahead of completed work, then use priority and approaching deadlines to determine which tasks deserve attention first.

### What I learned

Priority is not the same thing as deadline.

An email can be important without being immediately due, and an otherwise ordinary task can become urgent because its deadline is approaching.

Keeping those as separate fields made the ticket model more useful and easier to explain.

---

## Day 6 — Gmail Integration and External Data

### What I attempted

After the internal pipeline worked with demo data and `.eml` files, I connected Gmail as the external system that Chaos Inbox does not control.

The flow became:

`Gmail → Gmail adapter → EmailEnvelope → TicketProcessor`

I deliberately requested read-only mailbox access because Chaos Inbox does not need permission to send or delete email.

### What broke

Gmail integration introduced more configuration than the demo email source.

Authentication had to be treated separately from email processing, and the application could not assume that credentials would exist on every machine.

I also had to handle duplicate imports. Synchronising the same mailbox repeatedly should not create another copy of every existing ticket.

### How I handled it

I kept Gmail optional.

If Gmail credentials are unavailable, the rest of Chaos Inbox still works through demo emails and `.eml` imports.

I also store the external message identifier so that the application can recognise messages it has already imported.

### What I learned

This was a useful reminder that external integrations fail for reasons unrelated to my business logic.

The Gmail API can be unavailable, authentication can fail, credentials can be missing, or the returned message can contain a structure I did not expect.

The adapter boundary helped contain those problems.

---

## Day 7 — Docker, Portability and Testing

### What I attempted

At this point I had something that worked on my development machine, but one of the Build Week requirements was that somebody else had to be able to run it without me.

I added Docker support and tested the assumptions I had made about the environment.

I also added automated tests around the parts I considered most likely to fail.

Tests included cases such as:

* Urgent emails becoming high-priority tickets
* Deadline extraction
* Missing or strange input
* Chaos scores remaining within valid limits
* The fallback path continuing when AI cannot be used

### Docker problem

The biggest conceptual mistake was initially treating `localhost` as though it always meant my Windows machine.

When ASP.NET runs inside a container, `localhost` refers to that container.

The same problem applied to storage. SQLite inside a disposable container would disappear with the container unless its data was stored using a persistent volume.

### What I changed

I made environment-dependent settings configurable rather than hard-coded.

The containerised version uses persistent storage for SQLite and treats the AI runtime as a separately addressable service.

### What I learned

Docker was useful for more than packaging.

It exposed assumptions that were invisible while everything was running directly from Visual Studio.

"Works on my machine" became something I could actually investigate rather than just a joke.

---

## Day 8 — Side Quest, Model Runtime Comparison and Refinement

### Side quest

While Chaos Inbox was still being built, I spent approximately 45 minutes exploring **LM Studio** as an alternative to Ollama.

I wanted to understand whether I had chosen Ollama simply because I started with it or because it actually suited the project.

LM Studio felt more visual and exploratory. Browsing models and experimenting with them through a graphical interface was easier than doing everything from a terminal.

Ollama, however, still felt better suited to Chaos Inbox because I mainly needed the model runtime to behave like infrastructure behind my ASP.NET application.

The experiment also showed me that changing runtimes would not eliminate my main reliability problems.

Whether the model runs through Ollama or LM Studio, my application still needs to handle:

* Invalid structured output
* Model downtime
* Slow responses
* Incorrect classifications
* Missing deadlines
* Overconfident answers

I therefore decided not to switch Chaos Inbox to LM Studio.

### Refinement

I used the rest of the day to reduce friction in the dashboard and make important ticket information easier to identify.

I focused on showing:

* Priority
* Deadline
* Status
* Summary
* AI/fallback source
* Chaos score

rather than trying to make the application visually elaborate.

### What I learned

The side quest was useful because it made me justify a technology I had already selected.

My conclusion was not that LM Studio was worse. It was that it solved a slightly different developer problem.

LM Studio is something I would consider for model exploration. Ollama remained the simpler runtime for the current application architecture.

---

## Day 9 — Cleanup, Documentation and Demo Preparation

### What I attempted

The final day was deliberately not about adding another major feature.

I focused on making the repository understandable to someone who had never seen Chaos Inbox before.

I worked through:

* `README.md`
* `ARCHITECTURE.md`
* `LOG.md`
* `SIDEQUEST.md`
* Setup instructions
* Docker configuration
* `.gitignore`
* Sample `.eml` messages
* Tests
* Demo flow

I also checked the project for machine-specific paths, credentials and assumptions that would prevent another person from cloning and running it.

### What I deliberately did not add

The temptation on the final day was to keep adding features.

I decided against adding Outlook integration, attachment analysis or another AI feature.

At that point those additions would have increased the amount of code I needed to debug immediately before submission.

I chose to make the existing pipeline easier to run and explain instead.

### Demo preparation

I reduced the demonstration to a repeatable sequence:

`Import email → Create ticket → AI triage → Show priority/deadline → Change status → Inject failure → Disable model → Show fallback → Restart → Show persistence`

This lets me demonstrate both the product and the engineering decisions behind it.

### What I learned

The final day reinforced one of the main lessons from the project: finishing is an engineering decision too.

There will always be another feature that could be added. For this project, a smaller system whose behaviour I understand is more useful than a larger one containing features I cannot confidently explain.

---

# Bug That Cost the Most Time

### Symptom

The application behaved differently depending on whether it ran directly through Visual Studio or inside Docker. Persistence and service connectivity that appeared straightforward locally became less obvious inside a container.

### Wrong hypotheses

I initially focused on the application code and assumed that an ASP.NET setting or connection string was incorrect.

I also initially treated `localhost` as though it referred to the same machine from every execution environment.

### How I cornered it

Instead of changing several things at once, I separated the problem into individual questions:

1. Can ASP.NET start?
2. Can the application access SQLite?
3. Where is the SQLite database actually being created?
4. Can the application reach Ollama?
5. What does `localhost` mean from inside the container?
6. Does the database survive recreating the container?

Testing each assumption individually reduced the problem significantly.

### Actual cause

Containers have their own filesystem and networking context.

Resources available through a Windows `localhost` address or Windows filesystem path are not automatically the same resources seen from inside the Linux container.

### Fix

I made environment-specific addresses and paths configurable.

SQLite uses persistent storage in the containerised setup, and the AI runtime is treated as an external service rather than assuming it always exists at the application's own localhost address.

### What I learned

The bug changed how I think about configuration.

Hard-coded environment assumptions are dependencies even if they do not appear in NuGet.

---

# Where AI Genuinely Sped Me Up

AI was particularly useful when I encountered something unfamiliar.

Rather than asking it to build the entire application without understanding it, I found it more useful for narrower problems:

* Explaining APIs
* Comparing architecture options
* Understanding compiler errors
* Generating initial test cases
* Exploring Docker configuration
* Thinking through failure scenarios
* Designing structured model responses
* Reviewing code for edge cases

It shortened the time between encountering an unfamiliar concept and being able to investigate it productively.

---

# What I Cut and Why

I deliberately cut features that would increase scope without improving the core experiment.

**Production authentication:** The application demonstrates separation between users, but implementing a complete identity system correctly would introduce password management, authorization, recovery and additional security work.

**Outlook/Microsoft Graph:** The normalised email architecture makes another provider possible, but Gmail, `.eml` and demo inputs are enough to demonstrate provider independence.

**Attachment analysis:** Parsing PDFs, Word documents and images could become an entire project itself.

**Background Gmail polling:** Synchronisation remains user-triggered. Background processing would introduce scheduling, retry and deployment complexity.

**AI-generated replies:** Chaos Inbox focuses on turning incoming information into manageable work rather than automatically responding on behalf of users.

The decision was:

> Make fewer things fail well instead of making more things work only once.

---

# What I Would Do With Another Week

My first addition would not be another UI feature. I would build an evaluation harness.

I would create a labelled dataset of emails containing an expected:

* Priority
* Deadline
* Summary
* Action
* Category

Then I could measure:

`Priority accuracy | Deadline accuracy | Invalid-output rate | Human correction rate | Inference time | Fallback rate`

I would also use human ticket corrections as evaluation data.

If users repeatedly change a particular type of email from `High` to `Medium`, that provides evidence that either the prompt, model or fallback rules need adjustment.

After that, I would investigate proper authentication, encrypted OAuth token storage, background synchronisation, Outlook integration and attachment processing.

---

# What I Still Do Not Understand

The part I still do not completely understand is **confidence calibration**.

A model can return:

`confidence = 0.91`

but that does not mean it is actually correct 91% of the time.

I would like to understand how to calibrate that value against labelled emails and human corrections so that the confidence displayed in Chaos Inbox has measurable meaning.

I am also still interested in where the correct boundary lies between deterministic logic and model reasoning.

Rules are predictable and easy to test. Models are much better at interpreting ambiguous language.

I do not think this project proves exactly where that boundary should be, and I would rather leave that as an open question than pretend Build Week solved it.

---

# Final Reflection

Chaos Inbox started as an AI email organiser.

The more I built it, however, the more interesting question became:

> **What happens when the email is weird, the external API fails, the model is unavailable, or the AI confidently returns something I cannot trust?**

That question changed the shape of the project.

Instead of treating AI as the application, I ended up treating it as one unreliable component inside a larger system.

The final project therefore became less about proving that an LLM can summarise an email and more about exploring how to build useful software around AI **without assuming that the AI, the data or the network will always behave correctly.**
