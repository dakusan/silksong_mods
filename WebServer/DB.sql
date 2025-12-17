DROP TABLE IF EXISTS SilkSongItems;
CREATE TABLE SilkSongItems (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  ItemID int UNSIGNED NOT NULL,
  ItemName varchar(100) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  Username varchar(50) NOT NULL,
  CreatedAt TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY(ID),
  KEY (ItemID),
  KEY (ItemName),
  KEY (Username)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Categories;
CREATE TABLE Categories (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryGroup enum("Points of Interest", "Locations", "Collectibles", "Items", "Equipment", "Enemies", "Quests", "Other") CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  OrderNum tinyint UNSIGNED NOT NULL,
  IconID tinyint UNSIGNED NOT NULL,
  Title varchar(50) NOT NULL,
  Info varchar(255) NULL,
  PRIMARY KEY (ID),
  UNIQUE KEY (CategoryGroup, OrderNum),
  KEY (Title)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Items;
CREATE TABLE Items (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryID int UNSIGNED NOT NULL,
  Title varchar(100) NOT NULL,
  x double NOT NULL,
  y double NOT NULL,
  IconID tinyint UNSIGNED NULL,
  Reqs varchar(512) NULL,
  Needs varchar(255) NULL,
  Rewards varchar(255) NULL,
  Effect varchar(255) NULL,
  Tip varchar(255) NULL,
  Notes varchar(255) NULL,
  IgnPageName varchar(100) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
  Store mediumtext NULL,
  ImageURLs mediumtext NULL,
  PRIMARY KEY (ID),
  FOREIGN KEY (CategoryID) REFERENCES Categories (ID),
  KEY (Title)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;