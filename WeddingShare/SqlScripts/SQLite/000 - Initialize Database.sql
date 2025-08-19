--
-- Table structure for table `galleries`
--
DROP TABLE IF EXISTS `galleries`;
CREATE TABLE `galleries` (
  `id` INTEGER NOT NULL PRIMARY KEY,
  `name` TEXT NOT NULL UNIQUE,
  `secret_key` TEXT NULL
);

DROP TABLE IF EXISTS `gallery_items`;
CREATE TABLE `gallery_items` (
  `id` INTEGER NOT NULL PRIMARY KEY,
  `gallery_id` INTEGER NOT NULL,
  `title` TEXT NOT NULL,
  `uploaded_by` TEXT NULL,
  `state` INTEGER NOT NULL DEFAULT `0`,
  FOREIGN KEY (`gallery_id`) REFERENCES `galleries` (`id`) 
);

INSERT INTO `galleries` 
	(`id`, `name`, `secret_key`)
VALUES 
	(0, 'all', NULL),
	(1, 'default', NULL);

--
-- Table structure for table `users`
--
DROP TABLE IF EXISTS `users`;
CREATE TABLE `users` (
  `id` INTEGER NOT NULL PRIMARY KEY,
  `username` TEXT NOT NULL UNIQUE,
  `email` TEXT NULL UNIQUE,
  `password` TEXT NOT NULL
);

INSERT INTO `users` 
VALUES 
	(1,'admin', NULL, 'admin');