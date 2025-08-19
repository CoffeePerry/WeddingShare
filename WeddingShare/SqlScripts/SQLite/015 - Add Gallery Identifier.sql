--
-- Add column `identifier` to `galleries`
--
ALTER TABLE `galleries` ADD `identifier` TEXT NOT NULL DEFAULT '';
UPDATE `galleries` SET `identifier`=`name`;