--
-- Wipe previously existing tables
--
DROP TABLE IF EXISTS `gallery_settings`;
DROP TABLE IF EXISTS `gallery_items`;
DROP TABLE IF EXISTS `galleries`;
DROP TABLE IF EXISTS `custom_resources`;
DROP TABLE IF EXISTS `audit_logs`;
DROP TABLE IF EXISTS `settings`;
DROP TABLE IF EXISTS `users`;

--
-- Table structure for table `galleries`
--
CREATE TABLE `galleries` (
  `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
  `name` VARCHAR(50) NOT NULL UNIQUE,
  `secret_key` VARCHAR(100) NULL
);

CREATE TABLE `gallery_items` (
  `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
  `gallery_id` BIGINT NOT NULL,
  `title` VARCHAR(50) NOT NULL,
  `uploaded_by` VARCHAR(50) NULL,
  `state` INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (`gallery_id`) REFERENCES `galleries` (`id`) 
);

INSERT INTO `galleries` 
	(`id`, `name`, `secret_key`)
VALUES
	(1, 'default', NULL);

--
-- Table structure for table `users`
--
CREATE TABLE `users` (
  `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
  `username` VARCHAR(50) NOT NULL UNIQUE,
  `email` VARCHAR(100) NULL UNIQUE,
  `password` VARCHAR(100) NOT NULL
);

INSERT INTO `users` 
VALUES 
	(1,'admin', NULL, 'admin');