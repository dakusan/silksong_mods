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