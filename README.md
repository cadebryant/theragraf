# TheraGraf

**Theragraf** is an open-source, agentic clinical documentation engine designed to eliminate the "paperwork tax" for occupational therapists, physical therapists, and mental health practitioners.

Built with a privacy-first philosophy, Theragraf uses a **Bring-Your-Own-Key (BYOK)** architecture to ensure your patient data never touches our servers—you maintain full control of your clinical records.

---

## 🛠 Why Theragraf?

Modern clinical documentation is broken. Current solutions are high-cost, closed-source, and create data silos. Theragraf changes the paradigm:

* **Privacy-First:** Your patient data stays between your device and your chosen AI provider (OpenAI, Anthropic, or Local Ollama).
* **Cost-Efficient:** Pay only for the tokens you consume. No $100+/mo subscriptions.
* **Agentic Workflow:** Not just a scribe, but an intelligent pipeline that cleans transcripts, structures SOAP notes, and validates compliance against clinical standards.
* **Clinician-Centric:** Built for professionals who value precision, auditability, and data ownership.

---

## 🏗 System Architecture

Theragraf utilizes an agent-driven pipeline built on **Azure Functions (.NET 8 Isolated)**, managed by **Durable Functions** for reliable, stateful orchestration.

### The Agentic Pipeline:
1.  **Ingestion Agent:** Cleans raw transcripts and extracts core clinical observations.
2.  **SOAP Agent:** Transforms observations into professional, structured clinical notes.
3.  **Compliance Agent:** Validates clinical documentation against insurance and billing standards.
4.  **Finalizer Agent:** Formats reports for EHR/EHR-export.

---

## 🚀 Getting Started

### Prerequisites
* **Azure Functions Core Tools**
* **.NET 8.0 SDK**
* **Azurite** (for local state management)

### Local Development
1. Clone the repository: `git clone https://github.com/your-username/theragraf.git`
2. Configure your `local.settings.json` with your AI provider API key.
3. Launch the solution: 
   ```bash
   func start
