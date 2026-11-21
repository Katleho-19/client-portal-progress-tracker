# Client Portal & Progress Tracker

A lightweight, mobile-first client portal and deposit tracking system built with **C# ASP.NET Core**, **Supabase**, and the **Paystack API**. Designed for solo freelancers, it eliminates awkward payment chasing and scope creep by bringing quote approvals, deposit payments, and live project milestones into a single branded link—with zero client login required.

---

## Why This Project?
Solo freelancers often rely on static PDF quotes, manual bank details sent over WhatsApp, and endless follow-up messages to collect deposits or update clients on progress. **Client Portal & Progress Tracker** turns that fragmented workflow into a single, trustworthy link that protects both sides:
* **For Freelancers:** Locks project scope upon signature, automates deposit/balance tracking, and provides a clean admin dashboard to manage projects and verify manual EFT proofs.
* **For Clients:** Offers transparent itemized milestones, instant downloadable PDF quotes, secure ZAR checkout (Card, Instant EFT, SnapScan, or Manual EFT), and live progress tracking without needing to remember another password.

---

## Core Features (MVP v1.0)
* **Freelancer Authentication & Branding (`FR-01`, `FR-02`):** Secure registration, login, password management via Supabase Auth, and customizable business branding, contact details, and South African EFT banking credentials.
* **Passwordless Client Portal Links (`FR-04`, `FR-05`):** Generates clean, high-entropy shareable project links (`/portal/{slug}-{token}`) with a manual **Regenerate Link** security safety net to revoke and rotate leaked URLs.
* **ECTA-Compliant Typed E-Signatures (`FR-08`, `FR-09`, `NFR-03`):** Mandatory terms agreement checkbox and typed legal signature workflow backed by an immutable audit trail (IP address, User-Agent, and UTC timestamp) that locks project scope upon approval.
* **Automated PDF Quotations (`FR-10`):** Dynamically generates itemized PDF quotes via QuestPDF and stamps the client's signature and audit metadata once approved.
* **Dual ZAR Payment Support (`FR-11` – `FR-14`):** Supports automated online payments via **Paystack** (verified with HMAC SHA512 webhooks) alongside **Manual EFT** with Proof of Payment (POP) PDF upload and freelancer confirmation.
* **Live Milestone Tracking & Read-Only Archive (`FR-15`):** Real-time milestone status updates (`Pending` → `In_Progress` → `Completed`) that transition into a permanent read-only receipt archive once `balance_due` reaches `R0.00`.

---

## Tech Stack & Architecture
* **Backend & Web UI:** C# ASP.NET Core (3-Tier Monolithic Web Architecture)
* **Database, Auth & Storage:** Supabase (PostgreSQL, Row-Level Security, Supabase Auth, Private Storage Buckets)
* **Payment Gateway:** Paystack REST API & Webhooks (ZAR)
* **PDF Generation:** QuestPDF

---

## Engineering & Design Documentation
All requirements, UI prototypes, and UML system models are stored in the [`Documentations`](./Documentations) folder:
* [Software Requirements Specification (SRS)](./Documentations/Client%20Portal%20%26%20Deposit%20Tracker%20(Phase%202%20Final%20SRS).pdf)
* [Interactive Figma Prototype (Hi-Fi Wireframes)](https://www.figma.com/proto/GfBlpYxeQ12siRCBQE4mss/Client-Portal---Deposit-Tracker---Lo-fi-Wireframes?node-id=15-20&p=f&t=w54GC1z7kqDgEsGJ-1&scaling=min-zoom&content-scaling=fixed&page-id=15%3A2&starting-point-node-id=15%3A20&show-proto-sidebar=1)

---

## Project Roadmap
- [x] **Phase 1 — Planning & Requirements:** Finalized initial SRS (`15 FRs`, `5 NFRs`), data model, and feasibility & risk analysis.
- [x] **Phase 2 — System Architecture & Design:** Completed Lo-Fi & Hi-Fi Figma wireframes, 5-table ERD, UML Use Case diagram, and Paystack/EFT Sequence diagrams.
- [x] **Phase 3 — Database & Security Setup:** Deploy PostgreSQL schema, Row-Level Security (RLS) policies, and private POP storage bucket in Supabase.
- [x] **Phase 4 — Core Backend & Auth:** Initialize ASP.NET Core solution, Supabase Auth integration, and Freelancer Business Profile.
- [x] **Phase 5 — Client Portal & E-Signatures:** Build passwordless portal routing, ECTA audit logging, and QuestPDF quote stamping.
- [ ] **Phase 6 — Payments & Deployment:** Integrate Paystack checkout/webhooks, Manual EFT POP verification, and deploy live.

---

## Author
**Katleho Innocent Makhoali**  
*Software Development — C# ASP.NET Core | Supabase*
