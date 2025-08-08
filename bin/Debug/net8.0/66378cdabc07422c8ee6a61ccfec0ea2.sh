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

ps 91584;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 91584 > /dev/null;
done;

for child in $(list_child_processes 91593);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/66378cdabc07422c8ee6a61ccfec0ea2.sh;
