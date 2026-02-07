SET NAMES 'utf8mb4' COLLATE 'utf8mb4_general_ci';
SET FOREIGN_KEY_CHECKS=0;

#Set the comment (c) on a table (t). The comment has newlines trimmed.
CREATE PROCEDURE TempSetTableComment(t VARCHAR(255), c TEXT)
BEGIN
  SET @sql := CONCAT(
    'ALTER TABLE `', t, '` COMMENT = ', QUOTE(TRIM(BOTH '\n' FROM c))
  );
  PREPARE s FROM @sql;
  EXECUTE s;
  DEALLOCATE PREPARE s;
END;

DROP TABLE IF EXISTS SilkSongItems;
CREATE TABLE SilkSongItems (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  ItemID int UNSIGNED NOT NULL,
  ItemName varchar(100) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  Username varchar(50) NOT NULL,
  CreatedAt TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (ID),
  KEY (ItemID),
  KEY (ItemName),
  KEY (Username)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS CategoryGroups;
CREATE TABLE CategoryGroups (
  ID mediumint UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderNum int UNSIGNED NOT NULL,
  Title varchar(50) NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (OrderNum),
  UNIQUE KEY (Title)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Categories;
CREATE TABLE Categories (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryGroupID mediumint UNSIGNED NOT NULL,
  OrderNum tinyint UNSIGNED NOT NULL,
  IconID tinyint UNSIGNED NOT NULL,
  Title varchar(50) NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (CategoryGroupID, OrderNum),
  FOREIGN KEY (CategoryGroupID) REFERENCES CategoryGroups (ID),
  KEY (Title)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Items;
CREATE TABLE Items (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryID int UNSIGNED NOT NULL,
  x double NOT NULL,
  y double NOT NULL,
  IconID tinyint UNSIGNED NULL,
  ReqsSetID int UNSIGNED NULL,
  NeedsSetID int UNSIGNED NULL,
  RewardsSetID int UNSIGNED NULL,
  Title varchar(100) NOT NULL,
  WhereAt varchar(255) NULL,
  Notes varchar(550) NULL,
  Effect varchar(255) NULL,
  Tip varchar(255) NULL,

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
  Special varchar(100) NULL,
  AllowOn enum("All", "ReqOnly", "NeedOnly") CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT "All",

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
  FlagUnlinked bool NOT NULL DEFAULT false,
  FlagNot bool NOT NULL DEFAULT false,
  FlagStarted bool NOT NULL DEFAULT false,
  FlagRecommend bool NOT NULL DEFAULT false,

  #These are mutually excluive
  ItemID int UNSIGNED NULL,
  StaticLinkID int UNSIGNED NULL,
  Name varchar(125) NULL,

  SetID int UNSIGNED NOT NULL,
  GroupNum tinyint UNSIGNED NOT NULL DEFAULT 0,
  OrderNum tinyint UNSIGNED NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (SetID, GroupNum, OrderNum),
  FOREIGN KEY (ItemID) REFERENCES Items (ID),
  FOREIGN KEY (StaticLinkID) REFERENCES StaticLinks (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Stores;
CREATE TABLE Stores (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  VendorItemID int UNSIGNED NOT NULL,
  ReqsSetID int UNSIGNED NULL,
  NeedsSetID int UNSIGNED NOT NULL,
  RewardsSetID int UNSIGNED NOT NULL,
  OrderNum int UNSIGNED NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (VendorItemID, OrderNum),
  FOREIGN KEY (VendorItemID) REFERENCES Items (ID),
  FOREIGN KEY (ReqsSetID) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (NeedsSetID) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (RewardsSetID) REFERENCES ItemLinkDefs (SetID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ImageURLs;
CREATE TABLE ImageURLs (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  ItemID int UNSIGNED NOT NULL,
  OrderNum tinyint UNSIGNED NOT NULL,
  URL varchar(255) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (ItemID, OrderNum),
  FOREIGN KEY (ItemID) REFERENCES Items (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CALL TempSetTableComment('ImageURLs', '
Fills in ImageURLs for Items.
URL must be properly URL escaped.
Also See ImagePrefix section in Misc table comment.
');

DROP TABLE IF EXISTS OtherLinks;
CREATE TABLE OtherLinks (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  ItemID int UNSIGNED NOT NULL,
  OrderNum tinyint UNSIGNED NOT NULL,
  URL varchar(384) NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (ItemID, OrderNum),
  FOREIGN KEY (ItemID) REFERENCES Items (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CALL TempSetTableComment('OtherLinks', '
Fills in OtherLinks for Items.
URL:
  1) Must be properly URL escaped.
  2) Can be followed by an optional link name (URL escape not necessary) prefixed with a pipe “|”. If not given, the URL will be the link name.
  3) The Link name will have System.Net.WebUtility.UrlDecode() ran on it for display.
Also See OtherLinkPrefix section in Misc table comment.
');

DROP TABLE IF EXISTS IgnorePlayerNamedValues;
CREATE TABLE IgnorePlayerNamedValues (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  Name varchar(255) NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS MatchedIcons;
CREATE TABLE MatchedIcons (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  ItemID int UNSIGNED NOT NULL,
  ForStarting bool NOT NULL DEFAULT false,
  Parent varchar(255) NOT NULL,
  ValueName varchar(255) NOT NULL,

  PRIMARY KEY (ID),
  UNIQUE KEY (ItemID, ForStarting),
  UNIQUE KEY (Parent, ValueName),
  FOREIGN KEY (ItemID) REFERENCES Items (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ErrorLogs;
CREATE TABLE ErrorLogs (
  ID int UNSIGNED NOT NULL AUTO_INCREMENT,
  Username varchar(50) NOT NULL,
  CreatedAt TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  ErrorLog MEDIUMTEXT NOT NULL,

  PRIMARY KEY (ID),
  KEY (Username, ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Misc;
CREATE TABLE Misc (
  Section varchar(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  Name varchar(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  Value MEDIUMTEXT NOT NULL,
  Notes TINYTEXT NULL,

  PRIMARY KEY (Section, Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CALL TempSetTableComment('Misc', '
LinkColors section: Items directly tie to PharloomAtlas.DataStorage.LinkColorsT, so adding Names under that section that LinkColorsT does not have are ignored. They must be parsable by ColorUtility.TryParseHtmlString().

ImagePrefix and OtherLinkPrefix sections: These directly tie to PharloomAtlas.Item.{ImageURLs and Links} respectively. For entries in these lists:
- If it starts with a rule’s PrefixSymbol (.Name), remove the prefix and apply the rule’s regex rewrite.
- The rewrite specification is in .Value in the form: <D><SEARCH><D><REPLACE> (e.g., “~SEARCH~REPLACE”) where <D> is a single UTF-16 code unit delimiter.
- The SEARCH regex has no flags inherently. This means RegexOptions.CultureInvariant IS NOT turned on. Meaning \d matches more than [0-9].
- The delimiter must appear exactly twice (at the start and between SEARCH and REPLACE) and must not appear inside SEARCH or REPLACE.
');

INSERT INTO Misc VALUES
('LinkColors', 'Default',			'cyan',		'Default link color'),
('LinkColors', 'LinkHover',			'yellow',	'Color when a link has the mouse over it'),
('LinkColors', 'LabelHover',		'#4678C880','Box color for the entire label when mouse over (in the search box); Desaturated, mid-luminance blue goes well with: red, teal, plum, yellow, cyan, white, black, green'),
('LinkColors', 'Flag_NOT',			'red',		'Flag color (precedence=0) for NOT'),
('LinkColors', 'Flag_STARTED',		'teal',		'Flag color (precedence=1) for STARTED'),
('LinkColors', 'Flag_RECOMMENDED',	'#dda0dd',	'Flag color (precedence=2) for RECOMMENDED [#=plum]'),
('LinkColors', 'Sep_OR',			'purple',	'Separator for boolean OR “ OR ”'),
('LinkColors', 'Sep_AND',			'white',	'Separator for boolean AND “, ”'),
('LinkColors', 'Strike_Found',		'white',	'Straight line through link when item has been found'),
('LinkColors', 'Strike_Started',	'silver',	'Wavy line through link when item has been started (and not found)'),
('LinkColors', 'Search_Highlight',	'green',	'Highlighting searched string'),
('LinkColors', 'CollectedCounts',	'grey',		'Amounts the player has and needs to finish an item'),
('ImagePrefix',		'!',			'~^~https://ex.com/',
												'Replaces ! at the beginning of an ImageURL with: https://ex.com/'),
('OtherLinkPrefix',	'!',			'~^(.*)$~https://ex.com/Articles/$1|MySiteName $1',
												'Changes “!NAME” to “https://ex.com/Articles/NAME|MySiteName: NAME”');

DROP PROCEDURE TempSetTableComment;

SET FOREIGN_KEY_CHECKS=1;