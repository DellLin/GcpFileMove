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

ps 81615;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 81615 > /dev/null;
done;

for child in $(list_child_processes 81777);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net6.0/d8f8ed7a5d5c48a1bdc66a2d9927b41d.sh;
