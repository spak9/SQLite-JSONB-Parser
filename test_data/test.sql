PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE jsonb_data(type text, data blob);
INSERT INTO jsonb_data VALUES('null',X'00');
INSERT INTO jsonb_data VALUES('int',X'4331323334');
INSERT INTO jsonb_data VALUES('true',X'01');
INSERT INTO jsonb_data VALUES('false',X'02');
INSERT INTO jsonb_data VALUES('float',X'45332e3134');
INSERT INTO jsonb_data VALUES('text',X'c7196869206d79206e616d652069732073746576656e2070616b21');
INSERT INTO jsonb_data VALUES('text_empty',X'07');
INSERT INTO jsonb_data VALUES('emoji',X'47f09f92a9');
INSERT INTO jsonb_data VALUES('simple_object',X'cc1d4775736572cc165766697273746773746576656e476c6173743770616b');
INSERT INTO jsonb_data VALUES('empty_array',X'0b');
INSERT INTO jsonb_data VALUES('array',X'cb15133113321333133417351336133713381339233130');
COMMIT;

/*
sqlite> select json(data) from jsonb_data;
null
1234
true
false
3.14
"hi my name is steven pak!"
""
"💩"
{"user":{"first":"steven","last":"pak"}}
[]
[1,2,3,4,"5",6,7,8,9,10]
*
