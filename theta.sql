INSERT INTO dbo.DIM_TETA_USERS_SCD ([TETA_USER_ID], [UZYTKOWNIK], [PRAC_ID], [NAZWA_UZYTKOWNIKA_BD], [OSBY_ID], [USER_KIND],
                           [HAS_ACCESS_TO_TC], [HAS_ACCESS_TO_TG], [DOMAIN_USER_NAME], [DOMAIN_USER_SID], [USER_UNDER_LICENCE_TC_TN],
                           [USER_UNDER_LICENCE_TG_TN], [START_DATE], [END_DATE])
Select MERGE_OUT.[TETA_USER_ID], MERGE_OUT.[UZYTKOWNIK], MERGE_OUT.[PRAC_ID], MERGE_OUT.[NAZWA_UZYTKOWNIKA_BD], MERGE_OUT.[OSBY_ID], MERGE_OUT.[USER_KIND],
                    MERGE_OUT.[HAS_ACCESS_TO_TC], MERGE_OUT.[HAS_ACCESS_TO_TG], MERGE_OUT.[DOMAIN_USER_NAME], MERGE_OUT.[DOMAIN_USER_SID], MERGE_OUT.USER_UNDER_LICENCE_TC_TN,
                    MERGE_OUT.USER_UNDER_LICENCE_TG_TN, MERGE_OUT.[START_DATE], MERGE_OUT.[END_DATE]
FROM
(
MERGE dbo.DIM_TETA_USERS_SCD AS target
USING (
       Select TU.ID as TETA_USER_ID, TU.UZYTKOWNIK, TU.PRAC_ID, TU.NAZWA_UZYTKOWNIKA_BD, TU.OSBY_ID, TU.USER_KIND, TU.HAS_ACCESS_TO_TC, TU.HAS_ACCESS_TO_TG,
             TU.DOMAIN_USER_NAME, TU.DOMAIN_USER_SID, TU.USER_UNDER_LICENCE_TC_TN, TU.USER_UNDER_LICENCE_TG_TN, TU.SYSDATE from dbo.S_TETA_TETA_USERS TU
)AS Source(TETA_USER_ID, UZYTKOWNIK, PRAC_ID, NAZWA_UZYTKOWNIKA_BD, OSBY_ID, USER_KIND, HAS_ACCESS_TO_TC, HAS_ACCESS_TO_TG, DOMAIN_USER_NAME, DOMAIN_USER_SID,
                    USER_UNDER_LICENCE_TC_TN, USER_UNDER_LICENCE_TG_TN, SYSDATE)
       ON (target.TETA_USER_ID = Source.TETA_USER_ID)
       WHEN NOT MATCHED BY TARGET THEN
             INSERT (TETA_USER_ID, UZYTKOWNIK, PRAC_ID, NAZWA_UZYTKOWNIKA_BD, OSBY_ID, USER_KIND, HAS_ACCESS_TO_TC,
                                  HAS_ACCESS_TO_TG, DOMAIN_USER_NAME, DOMAIN_USER_SID, USER_UNDER_LICENCE_TC_TN, USER_UNDER_LICENCE_TG_TN,
                                  [START_DATE], [END_DATE])
             VALUES (source.TETA_USER_ID, source.UZYTKOWNIK, source.PRAC_ID, source.NAZWA_UZYTKOWNIKA_BD, source.OSBY_ID, source.USER_KIND, source.HAS_ACCESS_TO_TC,
                                  source.HAS_ACCESS_TO_TG, source.DOMAIN_USER_NAME, source.DOMAIN_USER_SID, source.USER_UNDER_LICENCE_TC_TN, source.USER_UNDER_LICENCE_TG_TN,
                                  Convert(date, source.SYSDATE), Convert(date, '9999-12-31', 120))
       WHEN NOT MATCHED BY SOURCE AND target.[END_DATE] = Convert(date, '9999-12-31', 120)
             THEN UPDATE SET target.[END_DATE] = DateAdd(dd, -1, Convert(date, GETDATE()))
       WHEN MATCHED AND target.[END_DATE] = Convert(date, '9999-12-31', 120)
                    AND (target.UZYTKOWNIK != source.UZYTKOWNIK
                                        or ISNULL(target.PRAC_ID, -1) != ISNULL(source.PRAC_ID, -1)
                                        or target.NAZWA_UZYTKOWNIKA_BD != source.NAZWA_UZYTKOWNIKA_BD
                                        or ISNULL(target.OSBY_ID, -1) != ISNULL(source.OSBY_ID, -1)
                                        or target.USER_KIND != source.USER_KIND
                                        or target.HAS_ACCESS_TO_TC != source.HAS_ACCESS_TO_TC
                                        or target.HAS_ACCESS_TO_TG != source.HAS_ACCESS_TO_TG
                                        or ISNULL(target.DOMAIN_USER_NAME, '') != ISNULL(source.DOMAIN_USER_NAME, '')
                                        or ISNULL(target.DOMAIN_USER_SID, '') != ISNULL(source.DOMAIN_USER_SID, '')
                                        or ISNULL(target.USER_UNDER_LICENCE_TC_TN, '') != ISNULL(source.USER_UNDER_LICENCE_TC_TN, '')
                                        or ISNULL(target.USER_UNDER_LICENCE_TG_TN, '') != ISNULL(source.USER_UNDER_LICENCE_TG_TN, '')
                           )
             THEN UPDATE SET target.[END_DATE] = DATEADD(DD, -1, CONVERT(Date, source.SYSDATE))
       OUTPUT $Action Action_Out, source.[TETA_USER_ID], source.[UZYTKOWNIK], source.[PRAC_ID], source.NAZWA_UZYTKOWNIKA_BD, source.OSBY_ID, source.USER_KIND,
                    source.HAS_ACCESS_TO_TC, source.HAS_ACCESS_TO_TG, source.DOMAIN_USER_NAME, source.DOMAIN_USER_SID, source.USER_UNDER_LICENCE_TC_TN,
                    source.USER_UNDER_LICENCE_TG_TN, Convert(date, source.SYSDATE) as [START_DATE], Convert(date, '9999-12-31', 120) as [END_DATE]
) AS MERGE_OUT
where MERGE_OUT.Action_Out = 'UPDATE' AND [TETA_USER_ID] is not null;
 
 
Delete from dbo.DIM_TETA_USERS_SCD where [START_DATE] > [END_DATE]
