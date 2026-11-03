using System;
using System.Collections.Generic;

namespace FaultTrace.Oracle.IntegrationTests.Infrastructure;

// The PL/SQL under test. A "-- @name" comment marks a line, so tests can expect exact line numbers.
internal static class OracleTestObjects
{
    public const string Pack = "FAULTTRACE_TEST_PACK";
    public const string Helper = "FAULTTRACE_TEST_HELPER";
    public const string Trigger = "FAULTTRACE_TEST_TRG";

    private const string PackSpec = """
        CREATE OR REPLACE PACKAGE FAULTTRACE_TEST_PACK AS
          PROCEDURE business;
          PROCEDURE business_multiline;
          PROCEDURE business_empty;
          PROCEDURE business_with_sqlerrm;
          PROCEDURE plain_stack;
          PROCEDURE nested_raise;
          PROCEDURE keep_errors;
          PROCEDURE drop_errors;
          PROCEDURE duplicated_prefix;
          PROCEDURE backtrace_labelled;
          PROCEDURE backtrace_plain;
          PROCEDURE through_helper;
          PROCEDURE required_insert;
          PROCEDURE required_update;
          PROCEDURE too_long;
          PROCEDURE too_large;
          PROCEDURE bad_number;
          PROCEDURE bad_date;
          PROCEDURE duplicate;
          PROCEDURE missing_parent;
          PROCEDURE has_children;
          PROCEDURE no_data;
          PROCEDURE too_many;
          PROCEDURE value_error;
          PROCEDURE divide_by_zero;
          PROCEDURE missing_table;
          PROCEDURE slow;
          PROCEDURE guarded_insert;
          PROCEDURE guarded_technical;
          FUNCTION business_value RETURN NUMBER;
        END;
        """;

    private const string PackBody = """
        CREATE OR REPLACE PACKAGE BODY FAULTTRACE_TEST_PACK AS
          PROCEDURE business IS
          BEGIN
            RAISE_APPLICATION_ERROR(-20001, 'Limit exceeded'); -- @business
          END;

          PROCEDURE business_multiline IS
          BEGIN
            RAISE_APPLICATION_ERROR(-20001, 'First line' || CHR(10) || 'Second line');
          END;

          PROCEDURE business_empty IS
          BEGIN
            RAISE_APPLICATION_ERROR(-20001, '');
          END;

          PROCEDURE business_with_sqlerrm IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL);
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20001, 'Failed: ' || SQLERRM);
          END;

          PROCEDURE level3 IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL); -- @level3
          END;

          PROCEDURE level2 IS
          BEGIN
            level3; -- @level2
          END;

          PROCEDURE plain_stack IS
          BEGIN
            level2; -- @level1
          END;

          PROCEDURE nested_level2 IS
          BEGIN
            level3;
          EXCEPTION
            WHEN OTHERS THEN
              RAISE; -- @nested2
          END;

          PROCEDURE nested_raise IS
          BEGIN
            nested_level2;
          EXCEPTION
            WHEN OTHERS THEN
              RAISE; -- @nested1
          END;

          PROCEDURE keep_errors IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL); -- @keep_insert
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20002, 'Could not save', TRUE); -- @keep_raise
          END;

          PROCEDURE drop_errors IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL);
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20002, 'Could not save'); -- @drop_raise
          END;

          PROCEDURE duplicated_prefix IS
          BEGIN
            business;
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20001, SQLERRM);
          END;

          PROCEDURE backtrace_labelled IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL); -- @labelled_insert
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20003, 'Could not save' || CHR(10) || 'BACKTRACE: ' || DBMS_UTILITY.FORMAT_ERROR_BACKTRACE);
          END;

          PROCEDURE backtrace_plain IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL); -- @plain_insert
          EXCEPTION
            WHEN OTHERS THEN
              RAISE_APPLICATION_ERROR(-20003, 'Could not save' || CHR(10) || DBMS_UTILITY.FORMAT_ERROR_BACKTRACE);
          END;

          PROCEDURE through_helper IS
          BEGIN
            FAULTTRACE_TEST_HELPER.fail('Limit exceeded'); -- @helper_call
          END;

          PROCEDURE required_insert IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, NULL);
          END;

          PROCEDURE required_update IS
          BEGIN
            UPDATE FAULTTRACE_TEST_PARENT SET NAME = NULL WHERE ID = 1;
          END;

          PROCEDURE too_long IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (2, 'much too long');
          END;

          PROCEDURE too_large IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME, AMOUNT) VALUES (2, 'a', 99999);
          END;

          PROCEDURE bad_number IS
            v NUMBER;
          BEGIN
            SELECT TO_NUMBER('abc') INTO v FROM DUAL;
          END;

          PROCEDURE bad_date IS
            v DATE;
          BEGIN
            SELECT TO_DATE('2026-01-01 10', 'YYYY-MM-DD') INTO v FROM DUAL;
          END;

          PROCEDURE duplicate IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (1, 'a');
          END;

          PROCEDURE missing_parent IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_CHILD (ID, PARENT_ID) VALUES (2, 999);
          END;

          PROCEDURE has_children IS
          BEGIN
            DELETE FROM FAULTTRACE_TEST_PARENT WHERE ID = 1;
          END;

          PROCEDURE no_data IS
            v NUMBER;
          BEGIN
            SELECT ID INTO v FROM FAULTTRACE_TEST_PARENT WHERE ID = 999;
          END;

          PROCEDURE too_many IS
            v NUMBER;
          BEGIN
            SELECT 1 INTO v FROM DUAL CONNECT BY LEVEL <= 2;
          END;

          PROCEDURE value_error IS
            v VARCHAR2(1);
          BEGIN
            v := 'too long';
          END;

          PROCEDURE divide_by_zero IS
            v NUMBER := 0;
          BEGIN
            v := 1 / v;
          END;

          PROCEDURE missing_table IS
          BEGIN
            EXECUTE IMMEDIATE 'DELETE FROM FAULTTRACE_TEST_MISSING';
          END;

          PROCEDURE slow IS
            v_end DATE := SYSDATE + 30 / 86400;
          BEGIN
            WHILE SYSDATE < v_end LOOP
              NULL;
            END LOOP;
          END;

          PROCEDURE guarded_insert IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_GUARDED (ID) VALUES (1);
          END;

          PROCEDURE guarded_technical IS
          BEGIN
            INSERT INTO FAULTTRACE_TEST_GUARDED (ID) VALUES (2);
          END;

          FUNCTION business_value RETURN NUMBER IS
          BEGIN
            RAISE_APPLICATION_ERROR(-20001, 'Limit exceeded'); -- @function
          END;
        END;
        """;

    private const string HelperSpec = """
        CREATE OR REPLACE PACKAGE FAULTTRACE_TEST_HELPER AS
          PROCEDURE fail(p_message VARCHAR2);
        END;
        """;

    private const string HelperBody = """
        CREATE OR REPLACE PACKAGE BODY FAULTTRACE_TEST_HELPER AS
          PROCEDURE fail(p_message VARCHAR2) IS
          BEGIN
            RAISE_APPLICATION_ERROR(-20001, p_message);
          END;
        END;
        """;

    private const string TriggerBody = """
        CREATE OR REPLACE TRIGGER FAULTTRACE_TEST_TRG
        BEFORE INSERT ON FAULTTRACE_TEST_GUARDED
        FOR EACH ROW
        BEGIN
          IF :NEW.ID = 2 THEN
            INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (3, NULL);
          END IF;
          RAISE_APPLICATION_ERROR(-20001, 'Row is locked');
        END;
        """;

    public static readonly string[] Statements =
    {
        "CREATE TABLE FAULTTRACE_TEST_PARENT (ID NUMBER CONSTRAINT FAULTTRACE_TEST_PARENT_PK PRIMARY KEY, NAME VARCHAR2(5) NOT NULL, AMOUNT NUMBER(3))",
        "CREATE TABLE FAULTTRACE_TEST_CHILD (ID NUMBER PRIMARY KEY, PARENT_ID NUMBER CONSTRAINT FAULTTRACE_TEST_CHILD_FK REFERENCES FAULTTRACE_TEST_PARENT (ID))",
        "CREATE TABLE FAULTTRACE_TEST_GUARDED (ID NUMBER)",
        "INSERT INTO FAULTTRACE_TEST_PARENT (ID, NAME) VALUES (1, 'a')",
        "INSERT INTO FAULTTRACE_TEST_CHILD (ID, PARENT_ID) VALUES (1, 1)",
        Normalize(HelperSpec),
        Normalize(HelperBody),
        Normalize(PackSpec),
        Normalize(PackBody),
        Normalize(TriggerBody),
    };

    public static readonly string[] DropStatements =
    {
        "DROP PACKAGE FAULTTRACE_TEST_PACK",
        "DROP PACKAGE FAULTTRACE_TEST_HELPER",
        "DROP TABLE FAULTTRACE_TEST_CHILD PURGE",
        "DROP TABLE FAULTTRACE_TEST_PARENT PURGE",
        "DROP TABLE FAULTTRACE_TEST_GUARDED PURGE",
    };

    public static Dictionary<string, int> FindLines()
    {
        var lines = new Dictionary<string, int>();
        var source = Normalize(PackBody).Split('\n');
        for (var index = 0; index < source.Length; index++)
        {
            var marker = source[index].IndexOf("-- @", StringComparison.Ordinal);
            if (marker >= 0)
            {
                lines[source[index].Substring(marker + 4).Trim()] = index + 1;
            }
        }

        return lines;
    }

    // Oracle counts lines by LF, and the checkout may use CRLF.
    private static string Normalize(string text) => text.Replace("\r\n", "\n");
}
