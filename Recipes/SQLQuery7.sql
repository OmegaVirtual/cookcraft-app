CREATE TABLE Recipes (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Title NVARCHAR(MAX) NOT NULL,
    ShortDescription NVARCHAR(MAX) NOT NULL,
    Instructions NVARCHAR(MAX) NOT NULL,
    ImageUrl NVARCHAR(MAX) NOT NULL,
    Ingredients NVARCHAR(MAX) NOT NULL,
    Allergens NVARCHAR(MAX) NOT NULL,
    ApplicationUserId NVARCHAR(450) NULL,
    CONSTRAINT FK_Recipes_AspNetUsers_ApplicationUserId FOREIGN KEY (ApplicationUserId)
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);
