# Project22: Laboratory Order Intake & Input Validation Service

A high-performance, robust C# class library built on **.NET 10** designed to ingest, parse, validate, and normalize laboratory order JSON requests. This project fulfills the technical assessment requirements for the Quality Engineering Internship at **Leica Biosystems (Danaher)**.

---

## 📌 Executive Summary & Architectural Overview

The core requirement of this service is to process incoming laboratory order JSON payloads and return a structured `OrderResult` containing:
1. **Status**: An enum (`Accepted` or `Rejected`).
2. **Order**: The parsed and normalized `Order` object (if `Accepted`), or `null` (if `Rejected`).
3. **Errors**: A list of `ValidationError` objects specifying the `Field`, `Code`, and `Message` for failing rules.

 ```mermaid
flowchart TD
    A[Laboratory Order JSON String] --> B[OrderIntakeService.Process]
    
    B --> C[Accepted]
    B --> D[Rejected]
    B --> E[Malformed Input]

    C --> C1["Status: Accepted<br/>Order: Populated<br/>Errors: Empty"]
    D --> D1["Status: Rejected<br/>Order: null<br/>Errors: Full Set"]
    E --> E1["Status: Rejected<br/>Order: null<br/>Errors: 1 x '$'"]
```

### Key Design Highlights:
* **Zero Unhandled Exceptions**: All inputs are safely parsed via `System.Text.Json.JsonDocument`. Structural JSON flaws or primitive/array type mismatches yield a controlled `MALFORMED_INPUT` error rather than throwing unexpected runtime exceptions.
* **Complete Error Accumulation**: If the JSON structure is valid, the validator evaluates all fields exhaustively and collects all failing validation rules into a list rather than short-circuiting on the first error.
* **Casing Normalization**: String values for controlled fields (`specimenType` and `priority`) are accepted in any casing combination (e.g., `bLoOd`, `uRgEnT`) and automatically converted to standard Title Case (`Blood`, `Urgent`).
* **Non-Strict Extensibility**: Unrecognized JSON attributes (such as `senderNote`) are ignored during intake, keeping the service flexible for future metadata additions without causing false rejections.

---

## 📂 Repository Structure

```text
Project22/
├── Project22.sln                  # Master .NET Solution file
├── README.md                      # Complete project documentation
├── .gitattributes                 # Git LFS configuration for .wmv media tracking
├── src/
│   └── OrderIntake/               # Class Library Project
│       ├── OrderIntake.csproj
│       ├── Models.cs              # Domain entities (Order, OrderResult, ValidationError, OrderStatus)
│       └── OrderIntakeService.cs  # Core parsing, validation, and normalization service
└── tests/
    └── OrderIntake.Tests/         # xUnit Test Suite
        ├── OrderIntake.Tests.csproj
        └── OrderIntakeTests.cs    # Automated tests spanning all 6 required assessment groups
