--
-- Add column `identifier` to `galleries`
--
ALTER TABLE `galleries` ADD `identifier` VARCHAR(50) NOT NULL;
UPDATE `galleries` SET `identifier`=`name`;
ALTER TABLE `galleries` ADD UNIQUE (`identifier`);