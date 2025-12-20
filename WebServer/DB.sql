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
  ReqsSetID int UNSIGNED NULL,
  NeedsSetID int UNSIGNED NULL,
  RewardsSetID int UNSIGNED NULL,
  Effect varchar(255) NULL,
  Tip varchar(255) NULL,
  Notes varchar(255) NULL,
  WhereAt varchar(1000) NULL,
  IgnPageName varchar(100) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
  Store mediumtext NULL,
  ImageURLs mediumtext NULL,

  PRIMARY KEY (ID),
  FOREIGN KEY (CategoryID) REFERENCES Categories (ID),
  FOREIGN KEY (ReqsSetID) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (NeedsSetID) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (RewardsSetID) REFERENCES ItemLinkDefs (SetID),
  KEY (Title)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS StaticLinks;
CREATE TABLE StaticLinks (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  Name varchar(100) NOT NULL,
  Special bool NOT NULL DEFAULT false,
  PRIMARY KEY (ID),
  KEY (Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS StaticLinkItems;
CREATE TABLE StaticLinkItems (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  StaticLinkID int UNSIGNED NOT NULL,
  ItemID int UNSIGNED NULL,
  CategoryID int UNSIGNED NULL,
  OrderNum tinyint UNSIGNED NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (StaticLinkID, ItemID),
  UNIQUE KEY (StaticLinkID, CategoryID),
  FOREIGN KEY (StaticLinkID) REFERENCES StaticLinks (ID),
  FOREIGN KEY (ItemID) REFERENCES Items (ID),
  FOREIGN KEY (CategoryID) REFERENCES Categories (ID),
  KEY (StaticLinkID, OrderNum)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ItemLinkDefs;
CREATE TABLE ItemLinkDefs (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  FlagAmount int UNSIGNED NOT NULL DEFAULT 1,
  FlagOptional bool NOT NULL DEFAULT false,
  FlagNot bool NOT NULL DEFAULT false,
  FlagStarted bool NOT NULL DEFAULT false,

  #These are mutually excluive
  ItemID int UNSIGNED NULL,
  StaticLinkID int UNSIGNED NULL,
  Name varchar(100) NULL,

  SetID int UNSIGNED NOT NULL,
  GroupNum tinyint UNSIGNED NOT NULL DEFAULT 0,
  OrderNum tinyint UNSIGNED NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (SetID, GroupNum, OrderNum),
  FOREIGN KEY (ItemID) REFERENCES Items (ID),
  FOREIGN KEY (StaticLinkID) REFERENCES StaticLinks (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;