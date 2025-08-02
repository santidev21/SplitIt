# SplitIt

**SplitIt** is a web application designed to help people manage shared expenses within groups. Whether you're on a trip with friends, splitting rent with roommates, or handling any shared bills, SplitIt simplifies the process of tracking expenses and settling debts fairly.

![Group Overview](docs/images/group-overview.png)
---

## ✨ Features

- 🧾 Create and manage expense groups
- 👥 Add participants to each group
- 💸 Register shared expenses and specify who paid
- 🔄 Automatically split expenses among members
- 📊 See how much each member owes or is owed
- ✅ Settle individual or total debts
- 🔐 Authentication system with protected routes

---

## 🏗️ Architecture

The backend is structured following a Clean Architecture pattern:

- **`SplitIt.API`** → API layer (Controllers, Endpoints, Middlewares)
- **`SplitIt.Application`** → Data Transfer Objects (DTOs), Application Services, Use Cases
- **`SplitIt.Domain`** → Core domain layer containing Entities, Value Objects, Domain Logic
- **`SplitIt.Infrastructure`** → Data persistence (Entity Framework Core), Migrations, External services integration
- **`SplitIt.Shared`** → Shared kernel for cross-cutting concerns and reusable components (currently empty)

---

## 🛠 Tech Stack

- **Frontend**: Angular, Angular Material, SCSS, Bootstrap
- **Backend**: .NET 8 Web API (C#), Clean Architecture
- **Database**: SQL Server (via Entity Framework Core)
- **Authentication**: JWT (JSON Web Tokens)

---

## 🚀 Getting Started

### Prerequisites

- Node.js and Angular CLI
- .NET 8 SDK
- SQL Server

### Frontend Setup
```bash
cd split-it-ui
npm install
ng serve
```

### Backend Setup
```bash
cd SplitIt.API
dotnet restore
dotnet ef database update
dotnet run
```

## 🖼️ Screenshots
### 🔹 Add group  
![Add group](docs/images/add-group.png)

### 🔹 Group Overview  
![Group Overview](docs/images/group-overview.png)

### 🔹 Add Expense Dialog  
![Add Expense](docs/images/add-expense.png)

## 📌 Future Features
- [ ] Add partial payments functionality  
- [ ] Support alternative split methods (by amount or percentage)  
- [ ] Email validation  
- [ ] Add group admin and application admin roles
