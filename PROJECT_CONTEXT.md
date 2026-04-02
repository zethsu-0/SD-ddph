# PROJECT CONTEXT — POS SYSTEM

## Overview
This project is a Point of Sale (POS) system built using C# in Visual Studio with SQL Server as the database.

The database was created manually using SQL queries.

---

## Database: ddph

### Users Table
Stores system users (admin and cashier).

```sql
CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Password NVARCHAR(255) NOT NULL,
    Role NVARCHAR(20) NOT NULL
);
```

**Fields:**
- Id (Primary Key, Auto Increment)
- Username (Unique)
- Password
- Role ("admin" or "cashier")

---

### Categories Table
Stores product categories.

```sql
CREATE TABLE Categories (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL
);
```

**Fields:**
- Id (Primary Key)
- Name (Category name)

---

### Products Table
Stores all products available for sale.

```sql
CREATE TABLE Products (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ProductName NVARCHAR(100) NOT NULL,
    CategoryId INT,
    Price DECIMAL(10,2) NOT NULL,
    Stock INT NOT NULL,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
```

**Fields:**
- Id (Primary Key)
- ProductName
- CategoryId (Foreign Key → Categories)
- Price
- Stock

---

### Sales Table
Stores transaction summaries.

```sql
CREATE TABLE Sales (
    Id INT PRIMARY KEY IDENTITY(1,1),
    TransactionDate DATETIME NOT NULL DEFAULT GETDATE(),
    CashierName NVARCHAR(50),
    TotalAmount DECIMAL(10,2) NOT NULL,
    Payment DECIMAL(10,2) NOT NULL,
    ChangeAmount DECIMAL(10,2) NOT NULL
);
```

**Fields:**
- Id (Primary Key)
- TransactionDate
- CashierName
- TotalAmount
- Payment
- ChangeAmount

---

### SaleItems Table
Stores individual items per transaction.

```sql
CREATE TABLE SaleItems (
    Id INT PRIMARY KEY IDENTITY(1,1),
    SaleId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    Subtotal DECIMAL(10,2) NOT NULL,
    FOREIGN KEY (SaleId) REFERENCES Sales(Id),
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
```

**Fields:**
- Id (Primary Key)
- SaleId (Foreign Key → Sales)
- ProductId (Foreign Key → Products)
- Quantity
- Price
- Subtotal

---

## Relationships

- One Category → Many Products
- One Sale → Many SaleItems
- One Product → Many SaleItems

---

## Business Rules

- Stock must be reduced after every sale
- Each sale must:
  1. Insert into Sales table
  2. Insert related records into SaleItems
  3. Update product stock
- All sale operations must use a SQL transaction

---

## Notes for the Agent

- Database: SQL Server (ddph)
- IDs are auto-incremented using IDENTITY
- Queries are written manually (not ORM)
- System is built in C# (WPF)
- Focus on reliability and simple UI for cashier use
