USE ddph;
GO

IF OBJECT_ID('dbo.Sales', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales (
        Id INT PRIMARY KEY IDENTITY(1,1),
        TransactionDate DATETIME NOT NULL DEFAULT GETDATE(),
        CashierName NVARCHAR(50) NULL,
        TotalAmount DECIMAL(10,2) NOT NULL,
        Payment DECIMAL(10,2) NOT NULL,
        ChangeAmount DECIMAL(10,2) NOT NULL
    );
END
GO

IF OBJECT_ID('dbo.SaleItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleItems (
        Id INT PRIMARY KEY IDENTITY(1,1),
        SaleId INT NOT NULL,
        ProductId INT NOT NULL,
        Quantity INT NOT NULL,
        Price DECIMAL(10,2) NOT NULL,
        Subtotal DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_SaleItems_Sales
            FOREIGN KEY (SaleId) REFERENCES dbo.Sales(Id),
        CONSTRAINT FK_SaleItems_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
    );
END
GO
