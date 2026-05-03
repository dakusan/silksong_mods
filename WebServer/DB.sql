SET NAMES 'utf8mb4' COLLATE 'utf8mb4_general_ci';
SET FOREIGN_KEY_CHECKS=0;

#Set the comment (c) on a table (t). The comment has newlines trimmed.
DELIMITER ;;
CREATE PROCEDURE TempSetTableComment(t VARCHAR(255), c TEXT)
BEGIN
  SET @sql := CONCAT(
    'ALTER TABLE `', t, '` COMMENT = ', QUOTE(TRIM(BOTH '\n' FROM c))
  );
  PREPARE s FROM @sql;
  EXECUTE s;
  DEALLOCATE PREPARE s;
END;;
DELIMITER ;

DROP TABLE IF EXISTS SilkSongItems;
CREATE TABLE SilkSongItems (
  ID        int UNSIGNED AUTO_INCREMENT                                 NOT NULL,
  ItemID    int UNSIGNED                                                NOT NULL,
  ItemName  varchar(100) CHARACTER SET ascii COLLATE ascii_general_ci   NOT NULL,
  Username  varchar(50)                                                 NOT NULL,
  CreatedAt TIMESTAMP(3) DEFAULT CURRENT_TIMESTAMP(3)                   NOT NULL,

  PRIMARY KEY (ID      ),
          KEY (ItemID  ),
          KEY (ItemName),
          KEY (Username)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS CategoryGroups;
CREATE TABLE CategoryGroups (
  ID        mediumint UNSIGNED AUTO_INCREMENT   NOT NULL,
  OrderNum  int UNSIGNED                        NOT NULL,
  Title     varchar(50)                         NOT NULL,

  PRIMARY KEY (ID       ),
  UNIQUE  KEY (OrderNum ),
  UNIQUE  KEY (Title    )
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Categories;
CREATE TABLE Categories (
  ID                int UNSIGNED AUTO_INCREMENT NOT NULL,
  CategoryGroupID   mediumint UNSIGNED          NOT NULL,
  OrderNum          tinyint UNSIGNED            NOT NULL,
  IconID            tinyint UNSIGNED            NOT NULL,
  Title             varchar(50)                 NOT NULL,

  PRIMARY KEY (ID                       ),
  UNIQUE  KEY (CategoryGroupID, OrderNum),
  FOREIGN KEY (CategoryGroupID          ) REFERENCES CategoryGroups (ID),
          KEY (Title                    )
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Items;
CREATE TABLE Items (
  ID            int UNSIGNED AUTO_INCREMENT NOT NULL,
  CategoryID    int UNSIGNED                NOT NULL,
  x             double                      NOT NULL,
  y             double                      NOT NULL,
  IconID        tinyint UNSIGNED            NULL,
  ReqsSetID     int UNSIGNED                NULL,
  NeedsSetID    int UNSIGNED                NULL,
  RewardsSetID  int UNSIGNED                NULL,
  Title         varchar(100)                NOT NULL,
  WhereAt       varchar(255)                NULL,
  Notes         varchar(550)                NULL,
  Effect        varchar(255)                NULL,
  Tip           varchar(255)                NULL,

  PRIMARY KEY (ID          ),
  FOREIGN KEY (CategoryID  ) REFERENCES Categories   (ID   ),
  FOREIGN KEY (ReqsSetID   ) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (NeedsSetID  ) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (RewardsSetID) REFERENCES ItemLinkDefs (SetID),
          KEY (Title       )
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS StaticLinks;
CREATE TABLE StaticLinks (
  ID        int UNSIGNED AUTO_INCREMENT                                               NOT NULL,
  Name      varchar(100)                                                              NOT NULL,
  Special   varchar(100)                                                              NULL,
  AllowOn   enum('All', 'ReqOnly', 'NeedOnly') CHARACTER SET ascii COLLATE ascii_bin  NOT NULL DEFAULT 'All',

  PRIMARY KEY (ID  ),
          KEY (Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS StaticLinkItems;
CREATE TABLE StaticLinkItems (
  ID            int UNSIGNED AUTO_INCREMENT NOT NULL,
  StaticLinkID  int UNSIGNED                NOT NULL,
  ItemID        int UNSIGNED                NULL,
  CategoryID    int UNSIGNED                NULL,
  OrderNum      tinyint UNSIGNED            NOT NULL,

  PRIMARY KEY (ID                       ),
  UNIQUE  KEY (StaticLinkID, ItemID     ),
  UNIQUE  KEY (StaticLinkID, CategoryID ),
  FOREIGN KEY (StaticLinkID             ) REFERENCES StaticLinks(ID),
  FOREIGN KEY (ItemID                   ) REFERENCES Items      (ID),
  FOREIGN KEY (CategoryID               ) REFERENCES Categories (ID),
          KEY (StaticLinkID, OrderNum   )
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ItemLinkDefs;
CREATE TABLE ItemLinkDefs (
  ID            int UNSIGNED AUTO_INCREMENT NOT NULL,
  FlagAmount    int UNSIGNED DEFAULT 1      NOT NULL,
  FlagUnlinked  bool         DEFAULT false  NOT NULL,
  FlagNot       bool         DEFAULT false  NOT NULL,
  FlagStarted   bool         DEFAULT false  NOT NULL,
  FlagRecommend bool         DEFAULT false  NOT NULL,

  #These are mutually exclusive
  ItemID        int UNSIGNED                NULL,
  StaticLinkID  int UNSIGNED                NULL,
  Name          varchar(125)                NULL,

  SetID         int UNSIGNED                NOT NULL,
  GroupNum      tinyint UNSIGNED DEFAULT 0  NOT NULL,
  OrderNum      tinyint UNSIGNED            NOT NULL,

  PRIMARY KEY (ID                       ),
  UNIQUE  KEY (SetID, GroupNum, OrderNum),
  FOREIGN KEY (ItemID                   ) REFERENCES Items       (ID),
  FOREIGN KEY (StaticLinkID             ) REFERENCES StaticLinks (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER ;;

CREATE TRIGGER ItemLinkDefs_BEFORE_INSERT
BEFORE INSERT ON ItemLinkDefs
FOR EACH ROW
BEGIN
  IF EXISTS (
    SELECT 1
    FROM ItemLinkDefs
    WHERE SetID = NEW.SetID
      AND GroupNum = NEW.GroupNum
      AND OrderNum = NEW.OrderNum
  ) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Duplicate (SetID, GroupNum, OrderNum)';
  END IF;
END ;;

CREATE TRIGGER ItemLinkDefs_BEFORE_UPDATE
BEFORE UPDATE ON ItemLinkDefs
FOR EACH ROW
BEGIN
  IF (NEW.SetID <> OLD.SetID OR NEW.GroupNum <> OLD.GroupNum OR NEW.OrderNum <> OLD.OrderNum)
     AND EXISTS (
       SELECT 1
       FROM ItemLinkDefs
       WHERE SetID = NEW.SetID
         AND GroupNum = NEW.GroupNum
         AND OrderNum = NEW.OrderNum
         AND ID <> OLD.ID
     )
  THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Duplicate (SetID, GroupNum, OrderNum)';
  END IF;
END ;;

DELIMITER ;

DROP TABLE IF EXISTS Stores;
CREATE TABLE Stores (
  ID            int UNSIGNED AUTO_INCREMENT NOT NULL,
  VendorItemID  int UNSIGNED                NOT NULL,
  ReqsSetID     int UNSIGNED                NULL,
  NeedsSetID    int UNSIGNED                NOT NULL,
  RewardsSetID  int UNSIGNED                NOT NULL,
  OrderNum      int UNSIGNED                NOT NULL,

  PRIMARY KEY (ID                    ),
  UNIQUE  KEY (VendorItemID, OrderNum),
  FOREIGN KEY (VendorItemID          ) REFERENCES Items        (ID),
  FOREIGN KEY (ReqsSetID             ) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (NeedsSetID            ) REFERENCES ItemLinkDefs (SetID),
  FOREIGN KEY (RewardsSetID          ) REFERENCES ItemLinkDefs (SetID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ImageURLs;
CREATE TABLE ImageURLs (
  ID        int UNSIGNED AUTO_INCREMENT                                 NOT NULL,
  ItemID    int UNSIGNED                                                NOT NULL,
  OrderNum  tinyint UNSIGNED                                            NOT NULL,
  URL       varchar(255) CHARACTER SET ascii COLLATE ascii_general_ci   NOT NULL,

  PRIMARY KEY (ID               ),
  UNIQUE  KEY (ItemID, OrderNum ),
  FOREIGN KEY (ItemID           ) REFERENCES Items (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CALL TempSetTableComment('ImageURLs', '
Fills in ImageURLs for Items.
URL must be properly URL escaped.
Also See ImagePrefix section in Misc table comment.
');

DROP TABLE IF EXISTS OtherLinks;
CREATE TABLE OtherLinks (
  ID        int UNSIGNED AUTO_INCREMENT NOT NULL,
  ItemID    int UNSIGNED                NOT NULL,
  OrderNum  tinyint UNSIGNED            NOT NULL,
  URL       varchar(384)                NOT NULL,

  PRIMARY KEY (ID               ),
  UNIQUE  KEY (ItemID, OrderNum ),
  FOREIGN KEY (ItemID           ) REFERENCES Items (ID)
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
  ID    int UNSIGNED AUTO_INCREMENT NOT NULL,
  Name  varchar(255)                NOT NULL,

  PRIMARY KEY (ID  ),
  UNIQUE  KEY (Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS MatchedIcons;
CREATE TABLE MatchedIcons (
  ID            int UNSIGNED AUTO_INCREMENT NOT NULL,
  ItemID        int UNSIGNED                NOT NULL,
  ForStarting   bool         DEFAULT false  NOT NULL,
  Parent        varchar(255)                NOT NULL,
  ValueName     varchar(255)                NOT NULL,

  PRIMARY KEY (ID                   ),
  UNIQUE  KEY (ItemID, ForStarting  ),
  UNIQUE  KEY (Parent, ValueName    ),
  FOREIGN KEY (ItemID               ) REFERENCES Items (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS ErrorLogs;
CREATE TABLE ErrorLogs (
  ID        int UNSIGNED AUTO_INCREMENT                 NOT NULL,
  Username  varchar(50)                                 NOT NULL,
  CreatedAt TIMESTAMP(3) DEFAULT CURRENT_TIMESTAMP(3)   NOT NULL,
  ErrorLog  MEDIUMTEXT                                  NOT NULL,

  PRIMARY KEY (ID          ),
  KEY         (Username, ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Misc;
CREATE TABLE Misc (
  Section   varchar(40) CHARACTER SET ascii COLLATE ascii_bin   NOT NULL,
  Name      varchar(40) CHARACTER SET ascii COLLATE ascii_bin   NOT NULL,
  Value     MEDIUMTEXT                                          NOT NULL,
  Notes     TINYTEXT                                            NULL,

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

DROP TABLE IF EXISTS Scenes;
CREATE TABLE Scenes (
  ID                int UNSIGNED AUTO_INCREMENT NOT NULL,
  X                 double                      NOT NULL,
  Y                 double                      NOT NULL,
  Width             double                      NULL,
  Height            double                      NULL,
  ColorIndex        tinyint                     NOT NULL,
  Name              varchar(128)                NOT NULL,

  #The rest of these fields are used to determine map absolute positions for scene relative coordinates
  #X/Y=Start=ScenePos+BoundsSpriteSize*-0.5
  #End      =ScenePos+BoundsSpriteSize*+0.5
  #Width/Height=End-Start
  ScenePosX         double                      NOT NULL,
  ScenePosY         double                      NOT NULL,
  BoundsSpriteSizeX double                      NULL,
  BoundsSpriteSizeY double                      NULL,
  SceneLocalScaleX  double                      NULL,
  SceneLocalScaleY  double                      NULL,
  SceneSizeX        double                      NOT NULL,
  SceneSizeY        double                      NOT NULL,

  PRIMARY KEY (ID  ),
  UNIQUE  KEY (Name)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS TranslationSections;
CREATE TABLE TranslationSections (
  ID        int UNSIGNED AUTO_INCREMENT                                                                                                      NOT NULL ,
  Module    enum('PharloomAtlas', 'SilkDev', 'PinFinder', 'NoClip', 'VGAtlas', 'VGAtlas_Utils') CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  Section   varchar(50) CHARACTER SET ascii COLLATE ascii_general_ci                                                                         NOT NULL,
  `Order`   int UNSIGNED                                                                                                                     NOT NULL,
  Comment   varchar(100)                                                                                                                     NULL,

  PRIMARY KEY (ID             ),
  UNIQUE  KEY (Module, Section),
  UNIQUE  KEY (Module, `Order`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS TranslationKeys;
CREATE TABLE TranslationKeys (
  ID            int UNSIGNED AUTO_INCREMENT                                                                                                      NOT NULL,
  Module        enum('PharloomAtlas', 'SilkDev', 'PinFinder', 'NoClip', 'VGAtlas', 'VGAtlas_Utils') CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  Section       varchar(50) CHARACTER SET ascii COLLATE ascii_general_ci                                                                         NOT NULL,
  `Order`       int UNSIGNED                                                                                                                     NOT NULL,
  TKey          varchar(100)                                                                                                                     NOT NULL,
  `Default`     text                                                                                                                             NOT NULL,
  PreComment    text                                                                                                                             NULL,
  Comment       varchar(255)                                                                                                                     NULL,

  PRIMARY KEY (ID                       ),
  UNIQUE  KEY (Module, Section, `Order` ),
  UNIQUE  KEY (Module, Section, TKey    ),
          KEY (Module, `Order`          )
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS Translations;
CREATE TABLE Translations (
  ID                int UNSIGNED AUTO_INCREMENT NOT NULL,
  TranslationKeyID  int UNSIGNED                NOT NULL,
  Language          enum('ar', 'bn', 'de', 'en', 'es', 'fr', 'hi', 'id', 'it', 'ja', 'ko', 'mr', 'pt', 'ru', 'sw', 'ta', 'te', 'tr', 'ur', 'vi', 'zh') CHARACTER SET ascii COLLATE ascii_general_ci
                                                NOT NULL,
  Translation       text                        NOT NULL,

  PRIMARY KEY (ID                        ),
  UNIQUE  KEY (TranslationKeyID, Language),
  FOREIGN KEY (TranslationKeyID          ) REFERENCES TranslationKeys (ID)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE OR REPLACE VIEW TranslationsView AS
SELECT
	TK.ID AS TranslationKeyID, TK.Module, TK.Section, TK.TKey, TK.Default,
	MAX(CASE WHEN TR.Language='ar' THEN TR.Translation END) AS ar,
	MAX(CASE WHEN TR.Language='bn' THEN TR.Translation END) AS bn,
	MAX(CASE WHEN TR.Language='de' THEN TR.Translation END) AS de,
	MAX(CASE WHEN TR.Language='en' THEN TR.Translation END) AS en,
	MAX(CASE WHEN TR.Language='es' THEN TR.Translation END) AS es,
	MAX(CASE WHEN TR.Language='fr' THEN TR.Translation END) AS fr,
	MAX(CASE WHEN TR.Language='hi' THEN TR.Translation END) AS hi,
	MAX(CASE WHEN TR.Language='id' THEN TR.Translation END) AS id,
	MAX(CASE WHEN TR.Language='it' THEN TR.Translation END) AS it,
	MAX(CASE WHEN TR.Language='ja' THEN TR.Translation END) AS ja,
	MAX(CASE WHEN TR.Language='ko' THEN TR.Translation END) AS ko,
	MAX(CASE WHEN TR.Language='mr' THEN TR.Translation END) AS mr,
	MAX(CASE WHEN TR.Language='pt' THEN TR.Translation END) AS pt,
	MAX(CASE WHEN TR.Language='ru' THEN TR.Translation END) AS ru,
	MAX(CASE WHEN TR.Language='sw' THEN TR.Translation END) AS sw,
	MAX(CASE WHEN TR.Language='ta' THEN TR.Translation END) AS ta,
	MAX(CASE WHEN TR.Language='te' THEN TR.Translation END) AS te,
	MAX(CASE WHEN TR.Language='tr' THEN TR.Translation END) AS tr,
	MAX(CASE WHEN TR.Language='ur' THEN TR.Translation END) AS ur,
	MAX(CASE WHEN TR.Language='vi' THEN TR.Translation END) AS vi,
	MAX(CASE WHEN TR.Language='zh' THEN TR.Translation END) AS zh
FROM TranslationKeys AS TK
LEFT JOIN Translations AS TR ON TR.TranslationKeyID=TK.ID
GROUP BY TK.ID, TK.Order #Adding TK.Order here is just so the engine doesn’t complain about the ORDER BY
ORDER BY TK.Order ASC;

DROP PROCEDURE TempSetTableComment;

SET FOREIGN_KEY_CHECKS=1;