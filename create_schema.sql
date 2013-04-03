
DROP DATABASE IF EXISTS `skype`;
CREATE DATABASE `skype`;
USE `skype`;

CREATE TABLE `old_hype` (
  `url` varchar(511) NOT NULL,
  `handle` varchar(45) NOT NULL,
  `name` varchar(45) NOT NULL,
  `date` datetime NOT NULL,
  `body` text,
  PRIMARY KEY (`url`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

