--
-- Add column `identifier` to `galleries`
--
ALTER TABLE `galleries` DROP `identifier`;
ALTER TABLE `galleries` ADD `identifier` VARCHAR(50) NOT NULL UNIQUE DEFAULT '';
UPDATE `galleries` SET `identifier`=`name`;