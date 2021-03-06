


# 下载安装 5.6.40 rpm安装存在问题采用5.5

wget http://mirrors.sohu.com/mysql/MySQL-5.5/mysql-5.5.54-linux2.6-x86_64.tar.gz

cd /data/pack
rz
#mysql-5.5.54-linux2.6-x86_64.tar.gz

tar zvxf mysql-5.5.54-linux2.6-x86_64.tar.gz
mv mysql-5.5.54-linux2.6-x86_64 /usr/mysql


# 配置  /data 存在问题，先存 data
mv /etc/my.cnf /etc/my.cnf.bak
mkdir -p /data/mysql
vim /etc/my.cnf

`
[mysqld]
transaction-isolation = READ-COMMITTED
#Disabling symbolic-links is recommended to prevent assorted security risks;
#to do so, uncomment this line:
#symbolic-links = 0

key_buffer_size = 32M
max_allowed_packet = 32M
thread_stack = 256K
thread_cache_size = 64
query_cache_limit = 8M
query_cache_size = 64M
query_cache_type = 1

max_connections = 550
#expire_logs_days = 10
#max_binlog_size = 100M

#log_bin should be on a disk with enough free space. Replace '/var/lib/mysql/mysql_binary_log' with an appropriate path for your system
#and chown the specified folder to the mysql user.
log_bin=/var/lib/mysql/mysql_binary_log

datadir=/data/mysql

#For MySQL version 5.1.8 or later. For older versions, reference MySQL documentation for configuration help.
binlog_format = mixed

read_buffer_size = 2M
read_rnd_buffer_size = 16M
sort_buffer_size = 8M
join_buffer_size = 8M

#InnoDB settings
innodb_file_per_table = 1
innodb_flush_log_at_trx_commit  = 2
innodb_log_buffer_size = 64M
innodb_buffer_pool_size = 4G
innodb_thread_concurrency = 8
innodb_flush_method = O_DIRECT
innodb_log_file_size = 512M

[mysqld_safe]
log-error=/var/log/mysqld.log
pid-file=/var/run/mysqld/mysqld.pid

sql_mode=STRICT_ALL_TABLES

#solve Forcing close of thread xxx user: 'root'
skip-name-resolve

`


# 权限 非常重要
chown -R mysql.mysql /usr/mysql/ /data/mysql /var/lib/mysql/
chgrp -R mysql /usr/mysql/ /data/mysql /var/lib/mysql/
chmod -R ug+rwx /usr/mysql/ /data/mysql /var/lib/mysql/
 #chown mysql *；chgrp mysql *；chmod ug+rwx *

# 开机启动
cp -f /usr/mysql/support-files/mysql.server /etc/init.d/mysqld
chmod +x /etc/init.d/mysqld


# 初始化

/usr/mysql/scripts/mysql_install_db --basedir=/usr/mysql --datadir=/data/mysql/ --user=mysql

sed -i 's#/usr/local/mysql#/usr/mysql#g' /usr/mysql/bin/mysqld_safe /etc/init.d/mysqld

/etc/init.d/mysqld start


# 初始化数据库
/usr/mysql/bin/mysql_secure_installation
#dcjet


# 做链接
ln -s /usr/mysql/bin/mysql /usr/bin/mysql

# 外部访问权限
mysql -u root -p

use mysql;
update user set host='%' where user = 'root';
flush privileges; 



# 初始化业务库
mysql -u root -p

create database amon DEFAULT CHARACTER SET utf8;
grant all on amon.* TO 'amon'@'%' IDENTIFIED BY 'dcjet';

create database rman DEFAULT CHARACTER SET utf8;
grant all on rman.* TO 'rman'@'%' IDENTIFIED BY 'dcjet';

create database metastore DEFAULT CHARACTER SET utf8;
grant all on metastore.* TO 'hive'@'%' IDENTIFIED BY 'dcjet';

create database sentry DEFAULT CHARACTER SET utf8;
grant all on sentry.* TO 'sentry'@'%' IDENTIFIED BY 'dcjet';

create database nav DEFAULT CHARACTER SET utf8;
grant all on nav.* TO 'nav'@'%' IDENTIFIED BY 'dcjet';

create database navms DEFAULT CHARACTER SET utf8;
grant all on navms.* TO 'navms'@'%' IDENTIFIED BY 'dcjet';

create database oozie DEFAULT CHARACTER SET utf8;
grant all on oozie.* TO 'oozie'@'%' IDENTIFIED BY 'dcjet';

create database hue DEFAULT CHARACTER SET utf8;
grant all on hue.* TO 'hue'@'%' IDENTIFIED BY 'dcjet';

`
Activity Monitor	amon	amon	amon_password
Reports Manager	rman	rman	rman_password
Hive Metastore Server	metastore	hive	hive_password
Sentry Server	sentry	sentry	sentry_password
Cloudera Navigator Audit Server	nav	nav	nav_password
Cloudera Navigator Metadata Server	navms	navms	navms_password
`


# 安装mysql 客户端@所有机器
Install the JDBC driver on the Cloudera Manager Server host, as well as hosts to which you assign the Activity Monitor, Reports Manager, Hive Metastore Server, Hue Server, Sentry Server, Cloudera Navigator Audit Server, and Cloudera Navigator Metadata Server roles.


mkdir -p /data/pack
cd /data/pack
rz
#mysql-connector-java-8.0.11.tar.gz
tar zxvf  mysql-connector-java-8.0.11.tar.gz 
mkdir -p /usr/share/java/
cp mysql-connector-java-8.0.11/mysql-connector-java-8.0.11-bin.jar /usr/share/java/mysql-connector-java.jar
rm -rf mysql-connector-java-8.0.11