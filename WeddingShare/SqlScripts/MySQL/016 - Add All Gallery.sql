--
-- DROP "all" gallery if exists
--
DELETE FROM `galleries` WHERE `id`=0;

--
-- INSERT "all" gallery
--
INSERT INTO `galleries` 
	(`id`, `identifier`, `name`, `secret_key`, `owner`)
VALUES 
	(0, 'all', 'all', NULL, 0);