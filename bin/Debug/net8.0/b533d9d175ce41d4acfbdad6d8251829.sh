function list_child_processes () {
    local ppid=$1;
    local current_children=$(pgrep -P $ppid);
    local local_child;
    if [ $? -eq 0 ];
    then
        for current_child in $current_children
        do
          local_child=$current_child;
          list_child_processes $local_child;
          echo $local_child;
        done;
    else
      return 0;
    fi;
}

ps 6344;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 6344 > /dev/null;
done;

for child in $(list_child_processes 6351);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/b533d9d175ce41d4acfbdad6d8251829.sh;
