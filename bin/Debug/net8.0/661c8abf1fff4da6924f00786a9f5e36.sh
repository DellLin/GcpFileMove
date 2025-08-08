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

ps 89604;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 89604 > /dev/null;
done;

for child in $(list_child_processes 89611);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/661c8abf1fff4da6924f00786a9f5e36.sh;
